using System.Diagnostics;
using Amazon;
using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.Aws.Sqs.Abstractions;
using FxMap.Aws.Sqs.Extensions;
using FxMap.Kafka.Abstractions;
using FxMap.Kafka.Extensions;
using FxMap.Nats.Abstractions;
using FxMap.Nats.Extensions;
using FxMap.RabbitMq.Abstractions;
using FxMap.RabbitMq.Extensions;
using FxMap.Registries;
using FxMap.Tests.ProviderTests;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace FxMap.Tests.TransportTests;

/// <summary>A context that carries the token of the caller.</summary>
internal sealed class CancellableContext(CancellationToken token) : IContext
{
    public Dictionary<string, string> Headers { get; } = [];
    public CancellationToken CancellationToken => token;
}

/// <summary>
/// What every transport has to do when a server is stopped and started again (the supervisor does it with the strategies
/// OneForAll and RestForOne), when the caller cancels a request, and when the owner is slower than the timeout of the consumer.
/// </summary>
public static class LifecycleScenarios
{
    private static async Task<bool> WorksAsync(TransportHarness h, TimeSpan limit)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < limit)
        {
            var view = new TransportCustomerView { CustomerId = "c1" };
            try
            {
                await h.MapAsync(view);
                if (view.Name == "Alice") return true;
            }
            catch
            {
                // the server is not listening yet
            }

            await Task.Delay(300);
        }

        return false;
    }

    /// <summary>The server of an owner that nobody started: run it, stop it as the supervisor does, three times.</summary>
    internal static async Task A_server_can_be_stopped_and_started_again(TransportHarness h, IRequestServer server)
    {
        for (var round = 1; round <= 3; round++)
        {
            using var cts = new CancellationTokenSource();
            var run = Task.Run(() => server.StartAsync(cts.Token));
            (await WorksAsync(h, TimeSpan.FromSeconds(20))).ShouldBeTrue($"the server answers in round {round}");

            await cts.CancelAsync();
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (OperationCanceledException)
            {
                // the normal end of a cancelled server
            }

            await server.StopAsync(); // and it must not have failed to start (ObjectDisposedException) in a later round
        }
    }

    public static async Task A_caller_that_cancels_does_not_wait_for_the_owner(TransportHarness h)
    {
        (await WorksAsync(h, TimeSpan.FromSeconds(20))).ShouldBeTrue("works before");
        h.Owner.Delay = TimeSpan.FromSeconds(5);
        using var cts = new CancellationTokenSource();

        var clock = Stopwatch.StartNew();
        var request = h.MapAsync(new TransportCustomerView { CustomerId = "c1" }, new CancellableContext(cts.Token));
        await Task.Delay(300);
        await cts.CancelAsync();

        var error = await Should.ThrowAsync<Exception>(() => request);
        clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3), $"cancelled requests end at once, not at the end of the timeout ({error.GetType().Name})");
        (error is OperationCanceledException || error.InnerException is OperationCanceledException || error.ToString().Contains("anceled"))
            .ShouldBeTrue($"the error says that it was cancelled, but it is: {error}");

        h.Owner.Delay = TimeSpan.Zero;
        await Task.Delay(6000); // the late reply of the owner arrives to a caller that is gone
        (await WorksAsync(h, TimeSpan.FromSeconds(20))).ShouldBeTrue("works after the late reply of a cancelled request");
    }

    public static async Task A_request_slower_than_the_timeout_fails_at_the_timeout_and_the_next_one_is_not_mixed_up(
        TransportHarness h, TimeSpan timeout)
    {
        (await WorksAsync(h, TimeSpan.FromSeconds(20))).ShouldBeTrue("works before");
        h.Owner.Delay = timeout + TimeSpan.FromSeconds(2);

        var clock = Stopwatch.StartNew();
        await Should.ThrowAsync<Exception>(() => h.MapAsync(new TransportCustomerView { CustomerId = "c1" }));
        clock.Elapsed.ShouldBeGreaterThan(timeout - TimeSpan.FromMilliseconds(300));
        clock.Elapsed.ShouldBeLessThan(timeout + TimeSpan.FromSeconds(4), "the timeout of the consumer ends the request");

        // the late reply arrives while the owner is fast again: the answer for c2 must not be the one for c1
        h.Owner.Delay = TimeSpan.Zero;
        await Task.Delay(3000);
        var view = new TransportCustomerView { CustomerId = "c2" };
        await h.MapAsync(view);
        view.Name.ShouldBe("Bob");
        view.Email.ShouldBe("bob@example.com");
    }
}

[Collection(TransportTestsCollection.Name)]
public class ServerLifecycleTests(ITestOutputHelper output)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    // ---------------------------------------------------------------- NATS

    private static Action<MapConfigurator> Nats(string prefix) => cfg =>
        cfg.AddNats(c =>
        {
            c.TopicPrefix(prefix);
            c.NatsOpts(o => o.Url = TransportEnvironment.Get(TransportEnvironment.Nats));
        });

    private static Task<TransportHarness> StartNats(bool startOwner, TimeSpan? timeout = null)
    {
        var prefix = $"fxmaplife{Guid.NewGuid():N}";
        return TransportHarness.StartAsync(r => OwnerHosts.BrokerAsync(r, Nats(prefix), startOwner), _ => Nats(prefix), timeout);
    }

    [NatsFact]
    public async Task Nats_a_server_can_be_stopped_and_started_again()
    {
        await using var h = await StartNats(startOwner: false);
        var server = h.OwnerServices.GetRequiredService<INatsServer<ProviderCustomer, ProviderCustomerKey>>();

        await LifecycleScenarios.A_server_can_be_stopped_and_started_again(h, server);
    }

    [NatsFact]
    public async Task Nats_a_caller_that_cancels_does_not_wait_for_the_owner()
    {
        await using var h = await StartNats(startOwner: true);

        await LifecycleScenarios.A_caller_that_cancels_does_not_wait_for_the_owner(h);
    }

    [NatsFact]
    public async Task Nats_a_request_slower_than_the_timeout_fails_at_the_timeout()
    {
        await using var h = await StartNats(startOwner: true, Timeout);

        await LifecycleScenarios.A_request_slower_than_the_timeout_fails_at_the_timeout_and_the_next_one_is_not_mixed_up(h, Timeout);
    }

    // ---------------------------------------------------------------- RabbitMQ

    private static async Task<(TransportHarness Harness, RabbitMqTransportTests.VirtualHost VHost)> StartRabbit(
        bool startOwner, TimeSpan? timeout = null)
    {
        var endpoint = RabbitMqTransportTests.Endpoint.FromEnvironment();
        var vhost = await RabbitMqTransportTests.VirtualHost.CreateAsync(endpoint);
        Action<MapConfigurator> rabbit = cfg => cfg.AddRabbitMq(c => c.Host(endpoint.Host, vhost.Name, endpoint.AmqpPort));
        var harness = await TransportHarness.StartAsync(r => OwnerHosts.BrokerAsync(r, rabbit, startOwner), _ => rabbit, timeout);
        return (harness, vhost);
    }

    [RabbitMqFact]
    public async Task RabbitMq_a_server_can_be_stopped_and_started_again()
    {
        var (h, vhost) = await StartRabbit(startOwner: false);
        await using var _ = vhost;
        await using var __ = h;
        var server = h.OwnerServices.GetRequiredService<IRabbitMqServer>();

        await LifecycleScenarios.A_server_can_be_stopped_and_started_again(h, server);
    }

    [RabbitMqFact]
    public async Task RabbitMq_a_caller_that_cancels_does_not_wait_for_the_owner()
    {
        var (h, vhost) = await StartRabbit(startOwner: true);
        await using var _ = vhost;
        await using var __ = h;

        await LifecycleScenarios.A_caller_that_cancels_does_not_wait_for_the_owner(h);
    }

    [RabbitMqFact]
    public async Task RabbitMq_a_request_slower_than_the_timeout_fails_at_the_timeout()
    {
        var (h, vhost) = await StartRabbit(startOwner: true, Timeout);
        await using var _ = vhost;
        await using var __ = h;

        await LifecycleScenarios.A_request_slower_than_the_timeout_fails_at_the_timeout_and_the_next_one_is_not_mixed_up(h, Timeout);
    }

    // ---------------------------------------------------------------- SQS

    private static Action<MapConfigurator> Sqs(string account) => cfg =>
        cfg.AddSqs(s => s.Region(RegionEndpoint.USEast1, c =>
        {
            c.ServiceUrl(TransportEnvironment.Get(TransportEnvironment.Sqs));
            c.AccessKeyId(account);
            c.SecretAccessKey("test");
        }));

    private static Task<TransportHarness> StartSqs(bool startOwner, TimeSpan? timeout = null)
    {
        var account = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString();
        return TransportHarness.StartAsync(
            r => OwnerHosts.BrokerAsync(r, Sqs(account), startOwner, ready: TimeSpan.FromSeconds(3)), _ => Sqs(account), timeout);
    }

    [SqsFact]
    public async Task Sqs_a_server_can_be_stopped_and_started_again()
    {
        await using var h = await StartSqs(startOwner: false, Timeout);
        var server = h.OwnerServices.GetRequiredService<ISqsServer>();

        await LifecycleScenarios.A_server_can_be_stopped_and_started_again(h, server);
    }

    [SqsFact]
    public async Task Sqs_a_caller_that_cancels_does_not_wait_for_the_owner()
    {
        await using var h = await StartSqs(startOwner: true);

        await LifecycleScenarios.A_caller_that_cancels_does_not_wait_for_the_owner(h);
    }

    [SqsFact]
    public async Task Sqs_a_request_slower_than_the_timeout_fails_at_the_timeout()
    {
        await using var h = await StartSqs(startOwner: true, Timeout);

        await LifecycleScenarios.A_request_slower_than_the_timeout_fails_at_the_timeout_and_the_next_one_is_not_mixed_up(h, Timeout);
    }

    // ---------------------------------------------------------------- Kafka

    private static async Task<TransportHarness> StartKafka(bool startOwner, TimeSpan? timeout = null)
    {
        await KafkaTransportTests.ResetTopicsAsync();
        Action<MapConfigurator> kafka = cfg => cfg.AddKafka(c => c.Host(TransportEnvironment.Get(TransportEnvironment.Kafka)));
        return await TransportHarness.StartAsync(
            r => OwnerHosts.BrokerAsync(r, kafka, startOwner, ready: TimeSpan.FromSeconds(8)), _ => kafka, timeout);
    }

    [KafkaFact]
    public async Task Kafka_a_server_can_be_stopped_and_started_again()
    {
        await using var h = await StartKafka(startOwner: false);
        var server = h.OwnerServices.GetRequiredService<IKafkaServer<ProviderCustomer, ProviderCustomerKey>>();

        await LifecycleScenarios.A_server_can_be_stopped_and_started_again(h, server);
    }

    [KafkaFact]
    public async Task Kafka_a_caller_that_cancels_does_not_wait_for_the_owner()
    {
        await using var h = await StartKafka(startOwner: true);

        await LifecycleScenarios.A_caller_that_cancels_does_not_wait_for_the_owner(h);
    }

    [KafkaFact]
    public async Task Kafka_a_request_slower_than_the_timeout_fails_at_the_timeout()
    {
        await using var h = await StartKafka(startOwner: true, Timeout);

        await LifecycleScenarios.A_request_slower_than_the_timeout_fails_at_the_timeout_and_the_next_one_is_not_mixed_up(h, Timeout);
    }
}
