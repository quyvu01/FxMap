using System.Diagnostics;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>
/// SetRetryPolicy: how many attempts, when the delay is waited, what OnRetry and ShouldRetry receive, which exception is thrown, what
/// the caller cancelling does, and how it works with ThrowIfException.
/// </summary>
public class RetryPolicyTests
{
    /// <summary>A remote that fails its first <paramref name="failures"/> requests (boom1, boom2...) and records every event in order.</summary>
    private sealed class Scenario(int failures)
    {
        public List<string> Events { get; } = [];
        public int Attempts;

        public Action<FakeRemote> Remote => remote => remote.OnRequest = _ =>
        {
            var n = Interlocked.Increment(ref Attempts);
            Events.Add($"attempt {n}");
            if (n <= failures)
                throw new InvalidOperationException($"boom{n}");
            return Task.CompletedTask;
        };

        public Func<int, TimeSpan> Sleep(Func<int, TimeSpan> delay = null) => n =>
        {
            Events.Add($"delay for retry {n}");
            return delay?.Invoke(n) ?? TimeSpan.FromMilliseconds(5);
        };

        public Action<Exception, TimeSpan> OnRetry => (e, d) => Events.Add($"onRetry({e.Message}, {d.TotalMilliseconds:F0}ms)");
    }

    private static async Task<(FlatDto Dto, Exception Error)> Map(MappingHarness h, CancellationToken token = default)
    {
        var dto = new FlatDto { UserId = "u1" };
        try
        {
            await h.Map(dto, token);
            return (dto, null);
        }
        catch (Exception e)
        {
            return (dto, e);
        }
    }

    [Fact]
    public async Task Without_a_policy_a_failure_is_not_retried()
    {
        var s = new Scenario(failures: 5);
        using var h = MappingHarness.Create(c => c.ThrowIfException(), s.Remote);

        var (_, error) = await Map(h);

        error.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("boom1");
        s.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task A_request_that_works_is_not_retried_and_calls_nothing()
    {
        var s = new Scenario(failures: 0);
        using var h = MappingHarness.Create(c => c.SetRetryPolicy(3, s.Sleep(), s.OnRetry), s.Remote);

        var (dto, error) = await Map(h);

        error.ShouldBeNull();
        dto.UserName.ShouldBe("user-name:u1");
        s.Events.ShouldBe(["attempt 1"]);
    }

    [Fact]
    public async Task The_delay_is_waited_before_each_retry_and_the_request_works_after_the_failures()
    {
        var s = new Scenario(failures: 2);
        using var h = MappingHarness.Create(c => c.SetRetryPolicy(3, s.Sleep(), s.OnRetry), s.Remote);

        var (dto, error) = await Map(h);

        error.ShouldBeNull();
        dto.UserName.ShouldBe("user-name:u1");
        // attempt, the delay of the first retry, OnRetry with the exception that was just seen, then the retry
        s.Events.ShouldBe([
            "attempt 1", "delay for retry 1", "onRetry(boom1, 5ms)",
            "attempt 2", "delay for retry 2", "onRetry(boom2, 5ms)",
            "attempt 3"
        ]);
    }

    [Fact]
    public async Task RetryCount_is_the_number_of_retries_after_the_first_attempt()
    {
        var s = new Scenario(failures: 100);
        using var h = MappingHarness.Create(c => { c.ThrowIfException(); c.SetRetryPolicy(3, s.Sleep(), s.OnRetry); }, s.Remote);

        await Map(h);

        s.Attempts.ShouldBe(4);
    }

    [Fact]
    public async Task After_the_last_failure_nothing_is_waited_and_the_last_exception_is_thrown()
    {
        var s = new Scenario(failures: 100);
        using var h = MappingHarness.Create(c => { c.ThrowIfException(); c.SetRetryPolicy(2, s.Sleep(), s.OnRetry); }, s.Remote);

        var (_, error) = await Map(h);

        error.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("boom3"); // the last attempt, not the first
        s.Events.ShouldBe([
            "attempt 1", "delay for retry 1", "onRetry(boom1, 5ms)",
            "attempt 2", "delay for retry 2", "onRetry(boom2, 5ms)",
            "attempt 3" // no delay, no OnRetry: no retry follows
        ]);
    }

    [Fact]
    public async Task OnRetry_receives_the_delay_that_is_waited_and_the_retries_wait_it()
    {
        var s = new Scenario(failures: 2);
        var clock = Stopwatch.StartNew();
        using var h = MappingHarness.Create(
            c => c.SetRetryPolicy(3, s.Sleep(n => TimeSpan.FromMilliseconds(150 * n)), s.OnRetry), s.Remote);

        await Map(h);

        s.Events.ShouldContain("onRetry(boom1, 150ms)");
        s.Events.ShouldContain("onRetry(boom2, 300ms)");
        clock.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(440); // 150 + 300, minus the precision of the timer
    }

    [Fact]
    public async Task Without_a_sleep_provider_the_retries_follow_each_other_at_once()
    {
        var s = new Scenario(failures: 2);
        using var h = MappingHarness.Create(c => c.SetRetryPolicy(3, null, s.OnRetry), s.Remote);

        var (dto, error) = await Map(h);

        error.ShouldBeNull();
        dto.UserName.ShouldBe("user-name:u1");
        s.Events.ShouldBe(["attempt 1", "onRetry(boom1, 0ms)", "attempt 2", "onRetry(boom2, 0ms)", "attempt 3"]);
    }

    [Fact]
    public async Task RetryCount_zero_means_a_single_attempt()
    {
        var s = new Scenario(failures: 100);
        using var h = MappingHarness.Create(c => { c.ThrowIfException(); c.SetRetryPolicy(0, s.Sleep(), s.OnRetry); }, s.Remote);

        var (_, error) = await Map(h);

        error.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("boom1");
        s.Events.ShouldBe(["attempt 1"]);
    }

    [Fact]
    public void A_negative_RetryCount_is_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            MappingHarness.Create(c => c.SetRetryPolicy(-1)));
    }

    #region ShouldRetry

    [Fact]
    public async Task ShouldRetry_false_throws_the_exception_at_once_without_a_delay()
    {
        var s = new Scenario(failures: 100);
        using var h = MappingHarness.Create(
            c => { c.ThrowIfException(); c.SetRetryPolicy(3, s.Sleep(), s.OnRetry, shouldRetry: _ => false); }, s.Remote);

        var (_, error) = await Map(h);

        error.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("boom1");
        s.Events.ShouldBe(["attempt 1"]);
    }

    [Fact]
    public async Task ShouldRetry_receives_the_exception_and_can_retry_some_failures_only()
    {
        var seen = new List<string>();
        var attempts = 0;
        using var h = MappingHarness.Create(
            c => { c.ThrowIfException(); c.SetRetryPolicy(5, null, null, shouldRetry: e =>
            {
                seen.Add(e.GetType().Name + ":" + e.Message);
                return e is TimeoutException; // a timeout can go away, an argument error will come back
            }); },
            remote => remote.OnRequest = _ =>
            {
                var n = Interlocked.Increment(ref attempts);
                return n switch
                {
                    1 => throw new TimeoutException("slow"),
                    2 => throw new TimeoutException("slow again"),
                    _ => throw new ArgumentException("bad request")
                };
            });

        var (_, error) = await Map(h);

        error.ShouldBeOfType<ArgumentException>().Message.ShouldBe("bad request");
        attempts.ShouldBe(3); // two timeouts were retried, the argument error was not
        seen.ShouldBe(["TimeoutException:slow", "TimeoutException:slow again", "ArgumentException:bad request"]);
    }

    [Fact]
    public async Task ShouldRetry_is_not_asked_once_the_attempts_are_used_up()
    {
        var asked = 0;
        var s = new Scenario(failures: 100);
        using var h = MappingHarness.Create(
            c => { c.ThrowIfException(); c.SetRetryPolicy(2, null, null, shouldRetry: _ => { asked++; return true; }); }, s.Remote);

        await Map(h);

        s.Attempts.ShouldBe(3);
        asked.ShouldBe(2); // for the first and the second failure; the third has no retry left
    }

    #endregion

    #region Cancellation

    [Fact]
    public async Task A_caller_that_cancels_while_a_delay_is_waited_stops_the_retries()
    {
        var s = new Scenario(failures: 100);
        using var cts = new CancellationTokenSource();
        using var h = MappingHarness.Create(
            c => { c.ThrowIfException(); c.SetRetryPolicy(3, s.Sleep(_ => TimeSpan.FromSeconds(30)), s.OnRetry); }, s.Remote);

        var map = Map(h, cts.Token);
        while (!s.Events.Contains("onRetry(boom1, 30000ms)")) await Task.Delay(10);
        await cts.CancelAsync();
        var (_, error) = await map.WaitAsync(TimeSpan.FromSeconds(10));

        error.ShouldBeAssignableTo<OperationCanceledException>();
        s.Attempts.ShouldBe(1); // nothing was attempted after the cancellation
    }

    [Fact]
    public async Task A_token_that_was_cancelled_is_not_retried()
    {
        var attempts = 0;
        using var cts = new CancellationTokenSource();
        using var h = MappingHarness.Create(c => { c.ThrowIfException(); c.SetRetryPolicy(3); }, remote =>
            remote.OnRequest = async _ =>
            {
                Interlocked.Increment(ref attempts);
                await cts.CancelAsync();
                throw new OperationCanceledException(cts.Token);
            });

        var (_, error) = await Map(h, cts.Token);

        error.ShouldBeAssignableTo<OperationCanceledException>();
        attempts.ShouldBe(1);
    }

    #endregion

    #region With the suppression of the errors

    [Fact]
    public async Task Without_ThrowIfException_the_failures_are_retried_and_suppressed_after_the_last_one()
    {
        var s = new Scenario(failures: 100);
        using var h = MappingHarness.Create(c => c.SetRetryPolicy(2, null, s.OnRetry), s.Remote);

        var (dto, error) = await Map(h);

        error.ShouldBeNull(); // suppressed, as without a retry policy
        dto.UserName.ShouldBeNull();
        s.Attempts.ShouldBe(3); // but only after the retries
    }

    [Fact]
    public async Task Without_ThrowIfException_a_request_that_works_after_a_retry_gives_its_values()
    {
        var s = new Scenario(failures: 1);
        using var h = MappingHarness.Create(c => c.SetRetryPolicy(2, null, s.OnRetry), s.Remote);

        var (dto, error) = await Map(h);

        error.ShouldBeNull();
        dto.UserName.ShouldBe("user-name:u1");
        s.Attempts.ShouldBe(2);
    }

    #endregion
}
