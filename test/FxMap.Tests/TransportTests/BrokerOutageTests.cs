using System.Diagnostics;
using Amazon;
using FxMap.Aws.Sqs.Extensions;
using FxMap.Nats.Extensions;
using FxMap.RabbitMq.Extensions;
using FxMap.Registries;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace FxMap.Tests.TransportTests;

/// <summary>
/// What happens when the broker disappears and comes back, with real brokers behind a proxy that can be cut:
/// the owner has to listen again, the consumer has to fail (not hang) while the broker is away and work again afterwards.
/// </summary>
[Collection(TransportTestsCollection.Name)]
public class BrokerOutageTests(ITestOutputHelper output)
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(4);

    /// <summary>Maps a customer until it works, as a client that retries does; returns how long the broker was unusable.</summary>
    private static async Task<TimeSpan> MapUntilItWorksAsync(TransportHarness h, TimeSpan limit, ITestOutputHelper output)
    {
        var clock = Stopwatch.StartNew();
        Exception last = null;
        while (clock.Elapsed < limit)
        {
            var view = new TransportCustomerView { CustomerId = "c1" };
            try
            {
                await h.MapAsync(view);
                if (view.Name == "Alice") return clock.Elapsed;
                last = new InvalidOperationException($"mapped without an answer (Name = {view.Name ?? "null"})");
            }
            catch (Exception e)
            {
                last = e;
            }

            await Task.Delay(500);
        }

        output.WriteLine($"still failing after {limit}: {last}");
        throw new ShouldAssertException($"The request did not work again within {limit}: {last?.Message}");
    }

    // ---------------------------------------------------------------- NATS

    private static (string Host, int Port) NatsTarget()
    {
        var uri = new Uri(TransportEnvironment.Get(TransportEnvironment.Nats));
        return (uri.Host, uri.Port);
    }

    private static Action<MapConfigurator> Nats(string prefix, int port) => cfg =>
        cfg.AddNats(c =>
        {
            c.TopicPrefix(prefix);
            c.NatsOpts(o => o.Url = $"nats://127.0.0.1:{port}");
        });

    private static async Task<TransportHarness> StartNatsAsync(string prefix, int ownerPort, int consumerPort) =>
        await TransportHarness.StartAsync(r => OwnerHosts.BrokerAsync(r, Nats(prefix, ownerPort)),
            _ => Nats(prefix, consumerPort), RequestTimeout);

    [NatsFact]
    public async Task Nats_the_owner_listens_again_when_its_connection_to_the_broker_comes_back()
    {
        var (host, port) = NatsTarget();
        await using var ownerLink = BrokerProxy.To(host, port);
        await using var h = await StartNatsAsync($"fxmapoutage{Guid.NewGuid():N}", ownerLink.Port, port);
        await MapUntilItWorksAsync(h, TimeSpan.FromSeconds(10), output);

        ownerLink.Cut();
        await Task.Delay(3000);
        ownerLink.Restore();

        var recovery = await MapUntilItWorksAsync(h, TimeSpan.FromSeconds(60), output);
        output.WriteLine($"the owner answered again {recovery.TotalSeconds:F1} s after the broker came back");
    }

    [NatsFact]
    public async Task Nats_the_consumer_fails_while_the_broker_is_away_and_works_again_afterwards()
    {
        var (host, port) = NatsTarget();
        await using var consumerLink = BrokerProxy.To(host, port);
        await using var h = await StartNatsAsync($"fxmapoutage{Guid.NewGuid():N}", port, consumerLink.Port);
        await MapUntilItWorksAsync(h, TimeSpan.FromSeconds(10), output);

        consumerLink.Cut();
        var clock = Stopwatch.StartNew();
        await Should.ThrowAsync<Exception>(() => h.MapAsync(new TransportCustomerView { CustomerId = "c1" }));
        clock.Elapsed.ShouldBeLessThan(RequestTimeout + TimeSpan.FromSeconds(10), "a request must not hang while the broker is away");

        consumerLink.Restore();
        var recovery = await MapUntilItWorksAsync(h, TimeSpan.FromSeconds(60), output);
        output.WriteLine($"the consumer worked again {recovery.TotalSeconds:F1} s after the broker came back");
    }

    [NatsFact]
    public async Task Nats_a_request_in_flight_when_the_broker_goes_away_ends_within_the_timeout()
    {
        var (host, port) = NatsTarget();
        await using var ownerLink = BrokerProxy.To(host, port);
        await using var h = await StartNatsAsync($"fxmapoutage{Guid.NewGuid():N}", ownerLink.Port, port);
        await MapUntilItWorksAsync(h, TimeSpan.FromSeconds(10), output);
        h.Owner.Delay = TimeSpan.FromSeconds(2); // the owner is busy when the broker goes away

        var clock = Stopwatch.StartNew();
        var request = h.MapAsync(new TransportCustomerView { CustomerId = "c1" });
        await Task.Delay(500);
        ownerLink.Cut();

        await Should.ThrowAsync<Exception>(() => request);
        clock.Elapsed.ShouldBeLessThan(RequestTimeout + TimeSpan.FromSeconds(10), "a request in flight must not hang");

        h.Owner.Delay = TimeSpan.Zero;
        ownerLink.Restore();
        await MapUntilItWorksAsync(h, TimeSpan.FromSeconds(60), output);
    }

    // ---------------------------------------------------------------- RabbitMQ

    private static Action<MapConfigurator> RabbitMq(string vhost, int port) => cfg =>
        cfg.AddRabbitMq(c => c.Host("127.0.0.1", vhost, port));

    private sealed class RabbitRun(RabbitMqTransportTests.VirtualHost vhost, TransportHarness harness, params BrokerProxy[] links)
        : IAsyncDisposable
    {
        public TransportHarness Harness { get; } = harness;

        public async ValueTask DisposeAsync()
        {
            await Harness.DisposeAsync();
            foreach (var link in links) await link.DisposeAsync();
            await vhost.DisposeAsync();
        }
    }

    private static async Task<(RabbitRun Run, BrokerProxy OwnerLink, BrokerProxy ConsumerLink)> StartRabbitAsync()
    {
        var endpoint = RabbitMqTransportTests.Endpoint.FromEnvironment();
        var vhost = await RabbitMqTransportTests.VirtualHost.CreateAsync(endpoint);
        var ownerLink = BrokerProxy.To(endpoint.Host, endpoint.AmqpPort);
        var consumerLink = BrokerProxy.To(endpoint.Host, endpoint.AmqpPort);
        var harness = await TransportHarness.StartAsync(r => OwnerHosts.BrokerAsync(r, RabbitMq(vhost.Name, ownerLink.Port)),
            _ => RabbitMq(vhost.Name, consumerLink.Port), RequestTimeout);
        return (new RabbitRun(vhost, harness, ownerLink, consumerLink), ownerLink, consumerLink);
    }

    [RabbitMqFact]
    public async Task RabbitMq_the_owner_listens_again_when_its_connection_to_the_broker_comes_back()
    {
        var (run, ownerLink, _) = await StartRabbitAsync();
        await using var _ = run;
        await MapUntilItWorksAsync(run.Harness, TimeSpan.FromSeconds(10), output);

        ownerLink.Cut();
        await Task.Delay(3000);
        ownerLink.Restore();

        var recovery = await MapUntilItWorksAsync(run.Harness, TimeSpan.FromSeconds(60), output);
        output.WriteLine($"the owner answered again {recovery.TotalSeconds:F1} s after the broker came back");
    }

    [RabbitMqFact]
    public async Task RabbitMq_the_consumer_fails_while_the_broker_is_away_and_works_again_afterwards()
    {
        var (run, _, consumerLink) = await StartRabbitAsync();
        await using var _ = run;
        await MapUntilItWorksAsync(run.Harness, TimeSpan.FromSeconds(10), output);

        consumerLink.Cut();
        var clock = Stopwatch.StartNew();
        await Should.ThrowAsync<Exception>(() => run.Harness.MapAsync(new TransportCustomerView { CustomerId = "c1" }));
        clock.Elapsed.ShouldBeLessThan(RequestTimeout + TimeSpan.FromSeconds(10), "a request must not hang while the broker is away");

        consumerLink.Restore();
        var recovery = await MapUntilItWorksAsync(run.Harness, TimeSpan.FromSeconds(60), output);
        output.WriteLine($"the consumer worked again {recovery.TotalSeconds:F1} s after the broker came back");
    }

    [RabbitMqFact]
    public async Task RabbitMq_a_request_in_flight_when_the_broker_goes_away_ends_within_the_timeout()
    {
        var (run, ownerLink, _) = await StartRabbitAsync();
        await using var _ = run;
        await MapUntilItWorksAsync(run.Harness, TimeSpan.FromSeconds(10), output);
        run.Harness.Owner.Delay = TimeSpan.FromSeconds(2);

        var clock = Stopwatch.StartNew();
        var request = run.Harness.MapAsync(new TransportCustomerView { CustomerId = "c1" });
        await Task.Delay(500);
        ownerLink.Cut();

        await Should.ThrowAsync<Exception>(() => request);
        clock.Elapsed.ShouldBeLessThan(RequestTimeout + TimeSpan.FromSeconds(10), "a request in flight must not hang");

        run.Harness.Owner.Delay = TimeSpan.Zero;
        ownerLink.Restore();
        await MapUntilItWorksAsync(run.Harness, TimeSpan.FromSeconds(60), output);
    }

    // ---------------------------------------------------------------- SQS

    private static Action<MapConfigurator> Sqs(string account, int port) => cfg =>
        cfg.AddSqs(s => s.Region(RegionEndpoint.USEast1, c =>
        {
            c.ServiceUrl($"http://127.0.0.1:{port}");
            c.AccessKeyId(account);
            c.SecretAccessKey("test");
        }));

    private static async Task<(TransportHarness Harness, BrokerProxy OwnerLink, BrokerProxy ConsumerLink)> StartSqsAsync()
    {
        var uri = new Uri(TransportEnvironment.Get(TransportEnvironment.Sqs));
        var account = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString();
        var ownerLink = BrokerProxy.To(uri.Host, uri.Port);
        var consumerLink = BrokerProxy.To(uri.Host, uri.Port);
        var harness = await TransportHarness.StartAsync(
            r => OwnerHosts.BrokerAsync(r, Sqs(account, ownerLink.Port), ready: TimeSpan.FromSeconds(3)),
            _ => Sqs(account, consumerLink.Port), RequestTimeout);
        return (harness, ownerLink, consumerLink);
    }

    [SqsFact]
    public async Task Sqs_the_owner_listens_again_when_its_connection_to_the_service_comes_back()
    {
        var (h, ownerLink, consumerLink) = await StartSqsAsync();
        await using var __ = ownerLink;
        await using var ___ = consumerLink;
        await using var _ = h; // disposed first: the services stop while their links still work
        await MapUntilItWorksAsync(h, TimeSpan.FromSeconds(30), output);

        ownerLink.Cut();
        await Task.Delay(3000);
        ownerLink.Restore();

        var recovery = await MapUntilItWorksAsync(h, TimeSpan.FromSeconds(90), output);
        output.WriteLine($"the owner answered again {recovery.TotalSeconds:F1} s after the service came back");
    }

    [SqsFact]
    public async Task Sqs_the_consumer_fails_while_the_service_is_away_and_works_again_afterwards()
    {
        var (h, ownerLink, consumerLink) = await StartSqsAsync();
        await using var __ = ownerLink;
        await using var ___ = consumerLink;
        await using var _ = h; // disposed first: the services stop while their links still work
        await MapUntilItWorksAsync(h, TimeSpan.FromSeconds(30), output);

        consumerLink.Cut();
        var clock = Stopwatch.StartNew();
        await Should.ThrowAsync<Exception>(() => h.MapAsync(new TransportCustomerView { CustomerId = "c1" }));
        clock.Elapsed.ShouldBeLessThan(RequestTimeout + TimeSpan.FromSeconds(10), "a request must not hang while the service is away");

        consumerLink.Restore();
        var recovery = await MapUntilItWorksAsync(h, TimeSpan.FromSeconds(90), output);
        output.WriteLine($"the consumer worked again {recovery.TotalSeconds:F1} s after the service came back");
    }

    [SqsFact]
    public async Task Sqs_a_request_in_flight_when_the_service_goes_away_ends_within_the_timeout()
    {
        var (h, ownerLink, consumerLink) = await StartSqsAsync();
        await using var __ = ownerLink;
        await using var ___ = consumerLink;
        await using var _ = h; // disposed first: the services stop while their links still work
        await MapUntilItWorksAsync(h, TimeSpan.FromSeconds(30), output);
        h.Owner.Delay = TimeSpan.FromSeconds(2);

        var clock = Stopwatch.StartNew();
        var request = h.MapAsync(new TransportCustomerView { CustomerId = "c1" });
        await Task.Delay(500);
        ownerLink.Cut();

        await Should.ThrowAsync<Exception>(() => request);
        clock.Elapsed.ShouldBeLessThan(RequestTimeout + TimeSpan.FromSeconds(10), "a request in flight must not hang");

        h.Owner.Delay = TimeSpan.Zero;
        ownerLink.Restore();
        await MapUntilItWorksAsync(h, TimeSpan.FromSeconds(90), output);
    }
}
