using System.Collections.Concurrent;
using FxMap.Abstractions.Transporting;
using FxMap.Supervision;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Supervision;

/// <summary>A server whose behaviour is chosen per start attempt (1-based): throw, return, or run until it is cancelled.</summary>
internal sealed class FakeServer(Func<int, CancellationToken, Task> onStart = null) : IRequestServer
{
    private int _starts;
    private int _stops;
    public int Starts => _starts;
    public int Stops => _stops;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var attempt = Interlocked.Increment(ref _starts);
        return onStart is null ? Task.Delay(Timeout.Infinite, cancellationToken) : onStart(attempt, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _stops);
        return Task.CompletedTask;
    }

    /// <summary>Fails the first <paramref name="failures"/> attempts, then runs until it is cancelled.</summary>
    public static FakeServer FailsFirst(int failures, Exception exception = null) => new((n, ct) =>
        n <= failures ? Task.FromException(exception ?? new InvalidOperationException("boom")) : Task.Delay(Timeout.Infinite, ct));

    public static FakeServer AlwaysFails() => FailsFirst(int.MaxValue);
}

/// <summary>The supervisor of the servers of a transport: restart with backoff, limits, circuit breaker, strategies, stop.</summary>
public class ServerSupervisorTests
{
    private static SupervisorOptions Fast(Action<SupervisorOptions> configure = null)
    {
        var options = new SupervisorOptions
        {
            InitialBackoff = TimeSpan.FromMilliseconds(10),
            MaxBackoff = TimeSpan.FromMilliseconds(80),
            MaxRestartWindow = TimeSpan.FromMinutes(5),
            CircuitBreakerResetTime = TimeSpan.FromMilliseconds(400)
        };
        configure?.Invoke(options);
        return options;
    }

    private static async Task Eventually(Func<bool> condition, string because, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new ShouldAssertException($"Timeout waiting for: {because}");
            await Task.Delay(10);
        }
    }

    private static async Task<ServerSupervisor> StartedAsync(SupervisorOptions options, params (string Id, FakeServer Server)[] servers)
    {
        var supervisor = new ServerSupervisor(options);
        foreach (var (id, server) in servers) supervisor.RegisterServer(id, server);
        await supervisor.StartAsync();
        return supervisor;
    }

    [Fact]
    public async Task A_server_that_fails_is_restarted_and_becomes_healthy_again()
    {
        var server = FakeServer.FailsFirst(1);
        var supervisor = new ServerSupervisor(Fast());
        supervisor.RegisterServer("s", server);
        var changes = new ConcurrentQueue<(ServerHealth Previous, ServerHealth New)>();
        supervisor.ServerHealthChanged += (_, e) => changes.Enqueue((e.PreviousHealth, e.NewHealth));

        await using (supervisor)
        {
            await supervisor.StartAsync();
            await Eventually(() => server.Starts == 2, "the second start");
            await Eventually(() => supervisor.GetServerStates()["s"].Health == ServerHealth.Healthy,
                "healthy after the restart");

            changes.Select(c => c.New).ShouldBe([ServerHealth.Degraded, ServerHealth.Healthy]);
            supervisor.GetServerStates()["s"].LastException.ShouldBeOfType<InvalidOperationException>();
        }
    }

    [Fact]
    public async Task The_backoff_doubles_after_each_restart_and_stops_at_the_maximum()
    {
        var server = FakeServer.FailsFirst(6);
        await using var supervisor = await StartedAsync(Fast(o => o.EnableCircuitBreaker = false), ("s", server));

        await Eventually(() => server.Starts == 7, "the seventh start");

        // 10 -> 20 -> 40 -> 80 (the maximum): the backoff of the next restart is never above it
        supervisor.GetServerStates()["s"].CurrentBackoff.ShouldBe(TimeSpan.FromMilliseconds(80));
    }

    [Fact]
    public async Task A_server_is_stopped_for_good_after_the_maximum_of_restarts()
    {
        var server = FakeServer.AlwaysFails();
        await using var supervisor = await StartedAsync(
            Fast(o => { o.EnableCircuitBreaker = false; o.MaxRestarts = 2; }), ("s", server));

        await Eventually(() => supervisor.GetServerStates()["s"].Health == ServerHealth.Stopped, "stopped");
        await Task.Delay(300);

        server.Starts.ShouldBe(3); // the first start and 2 restarts
    }

    [Fact]
    public async Task The_circuit_opens_after_consecutive_failures_and_the_server_is_restarted_when_it_resets()
    {
        var server = FakeServer.AlwaysFails();
        await using var supervisor = await StartedAsync(
            Fast(o => { o.CircuitBreakerThreshold = 3; o.MaxRestarts = 100; }), ("s", server));

        await Eventually(() => supervisor.GetServerStates()["s"].Health == ServerHealth.CircuitOpen, "circuit open");
        var startsWhenOpened = server.Starts;
        startsWhenOpened.ShouldBe(3);

        await Task.Delay(150); // still inside CircuitBreakerResetTime (400 ms)
        server.Starts.ShouldBe(startsWhenOpened);

        await Eventually(() => server.Starts > startsWhenOpened, "a start after the circuit resets");
    }

    [Fact]
    public async Task A_server_that_recovers_after_the_circuit_reset_is_healthy_again()
    {
        var server = FakeServer.FailsFirst(3);
        await using var supervisor = await StartedAsync(Fast(o => o.CircuitBreakerThreshold = 3), ("s", server));

        await Eventually(() => server.Starts == 4, "the start after the circuit reset");
        await Eventually(() => supervisor.GetServerStates()["s"].Health == ServerHealth.Healthy, "healthy");
        supervisor.GetServerStates()["s"].CircuitBreakerResetTime.ShouldBeNull();
    }

    [Fact]
    public async Task A_cancelled_server_that_nobody_asked_to_stop_is_not_restarted()
    {
        var server = FakeServer.FailsFirst(1, new OperationCanceledException());
        await using var supervisor = await StartedAsync(Fast(), ("s", server));

        await Eventually(() => supervisor.GetServerStates()["s"].Health == ServerHealth.Stopped, "stopped");
        await Task.Delay(200);
        server.Starts.ShouldBe(1);
    }

    [Fact]
    public async Task The_directive_of_an_exception_decides_what_happens()
    {
        var resumed = FakeServer.FailsFirst(1, new TimeoutException());
        var escalated = FakeServer.FailsFirst(1, new OutOfMemoryException());
        var options = Fast(o => o.ExceptionDirectives[typeof(TimeoutException)] = SupervisorDirective.Resume);
        await using var supervisor = await StartedAsync(options, ("resumed", resumed), ("escalated", escalated));

        await Eventually(() => supervisor.GetServerStates()["escalated"].Health == ServerHealth.Stopped, "escalated");
        await Task.Delay(200);

        escalated.Starts.ShouldBe(1);
        resumed.Starts.ShouldBe(1); // Resume: the supervisor does not restart it
    }

    [Fact]
    public async Task OneForOne_restarts_only_the_server_that_failed()
    {
        var failing = FakeServer.FailsFirst(1);
        var other = new FakeServer();
        await using var supervisor = await StartedAsync(Fast(), ("a", failing), ("b", other));

        await Eventually(() => failing.Starts == 2, "restart of the failing server");
        await Task.Delay(200);

        other.Starts.ShouldBe(1);
        other.Stops.ShouldBe(0);
    }

    [Fact]
    public async Task OneForAll_restarts_every_server_once_without_a_cascade()
    {
        var failing = FakeServer.FailsFirst(1);
        var other = new FakeServer();
        await using var supervisor = await StartedAsync(Fast(o => o.Strategy = SupervisionStrategy.OneForAll),
            ("a", failing), ("b", other));

        await Eventually(() => other.Starts == 2, "restart of the other server");
        await Task.Delay(500); // a stopped server that is counted as a failure would restart them again

        failing.Starts.ShouldBe(2);
        other.Starts.ShouldBe(2);
        other.Stops.ShouldBe(1);
        supervisor.GetServerStates().Values.ShouldAllBe(s => s.Health == ServerHealth.Healthy);
    }

    [Fact]
    public async Task RestForOne_restarts_the_failed_server_and_the_ones_registered_after_it()
    {
        var before = new FakeServer();
        var failing = FakeServer.FailsFirst(1);
        var after = new FakeServer();
        await using var supervisor = await StartedAsync(Fast(o => o.Strategy = SupervisionStrategy.RestForOne),
            ("a", before), ("b", failing), ("c", after));

        await Eventually(() => after.Starts == 2, "restart of the later server");
        await Task.Delay(500);

        before.Starts.ShouldBe(1);
        failing.Starts.ShouldBe(2);
        after.Starts.ShouldBe(2);
    }

    [Fact]
    public async Task Stopping_the_supervisor_stops_every_server_and_nothing_is_restarted_afterwards()
    {
        var running = new FakeServer();
        var failing = FakeServer.AlwaysFails();
        var supervisor = await StartedAsync(Fast(o => o.EnableCircuitBreaker = false), ("a", running), ("b", failing));
        await Eventually(() => failing.Starts >= 2, "restarts");

        await supervisor.StopAsync();
        var startsAtStop = failing.Starts;
        await Task.Delay(300);

        running.Stops.ShouldBe(1);
        failing.Starts.ShouldBe(startsAtStop);
        await supervisor.DisposeAsync();
    }

    [Fact]
    public async Task Disposing_the_supervisor_while_the_circuit_is_open_does_not_restart_the_server()
    {
        var server = FakeServer.AlwaysFails();
        var supervisor = await StartedAsync(Fast(o => o.CircuitBreakerThreshold = 2), ("s", server));
        await Eventually(() => supervisor.GetServerStates()["s"].Health == ServerHealth.CircuitOpen, "circuit open");

        await supervisor.DisposeAsync();
        var starts = server.Starts;
        await Task.Delay(600); // longer than the reset time of the circuit

        server.Starts.ShouldBe(starts);
    }

    [Fact]
    public async Task Failures_that_are_far_apart_do_not_add_up_to_the_limits()
    {
        // The server runs well between its failures: after the window, the counters start again
        var server = new FakeServer(async (n, ct) =>
        {
            await Task.Delay(n <= 6 ? 150 : Timeout.Infinite, ct);
            if (n <= 6) throw new InvalidOperationException("boom");
        });
        await using var supervisor = await StartedAsync(
            Fast(o => { o.MaxRestartWindow = TimeSpan.FromMilliseconds(100); o.MaxRestarts = 2; o.CircuitBreakerThreshold = 2; }),
            ("s", server));

        await Eventually(() => server.Starts == 7, "the seventh start");
        await Eventually(() => supervisor.GetServerStates()["s"].Health == ServerHealth.Healthy, "healthy");
    }

    [Fact]
    public void A_server_id_cannot_be_registered_twice()
    {
        var supervisor = new ServerSupervisor(Fast());
        supervisor.RegisterServer("s", new FakeServer());

        Should.Throw<InvalidOperationException>(() => supervisor.RegisterServer("s", new FakeServer()));
    }
}
