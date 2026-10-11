using FxMap.Abstractions;
using FxMap.Azure.ServiceBus.Extensions;
using FxMap.Registries;
using Shouldly;
using Xunit;

namespace FxMap.Tests.TransportTests;

/// <summary>
/// The Azure Service Bus transport between two services, on the Service Bus emulator (see
/// <c>test/servicebus-emulator</c>; set FXMAP_TEST_SERVICEBUS to its connection string). The emulator has no management
/// API, so the queues are declared in its Config.json: a request queue and a reply queue (both require sessions) for each
/// prefix sbtest1..sbtest4, and each test uses a prefix of its own.
/// </summary>
[Collection(TransportTestsCollection.Name)]
public class ServiceBusTransportTests
{
    private static Action<MapConfigurator> ServiceBus(string prefix) => cfg =>
        cfg.AddAzureServiceBus(s =>
        {
            s.Host(TransportEnvironment.Get(TransportEnvironment.ServiceBus));
            s.TopicPrefix(prefix);
        });

    private static Task<TransportHarness> StartAsync(string prefix, bool startOwner = true, TimeSpan? timeout = null) =>
        TransportHarness.StartAsync(
            // Before it listens, the owner tries to create its queues with the administration client, which the emulator does
            // not serve: the SDK retries for about 10 s per queue and the warning is harmless (Config.json has the queues).
            r => OwnerHosts.BrokerAsync(r, ServiceBus(prefix), startOwner, ready: TimeSpan.FromSeconds(25)),
            _ => ServiceBus(prefix), timeout);

    private sealed class TestContext(Dictionary<string, string> headers) : IContext
    {
        public Dictionary<string, string> Headers { get; } = headers;
        public CancellationToken CancellationToken => CancellationToken.None;
    }

    [ServiceBusFact]
    public async Task The_consumer_is_filled_by_the_owner_through_the_queue()
    {
        await using var h = await StartAsync("sbtest1");
        var views = new[]
        {
            new TransportCustomerView { CustomerId = "c1" }, new TransportCustomerView { CustomerId = "c2" },
            new TransportCustomerView { CustomerId = "nobody" }
        };

        await h.MapAsync(views);

        views.Select(v => v.Name).ShouldBe(["Alice", "Bob", null]);
        views.Select(v => v.Email).ShouldBe(["alice@example.com", "bob@example.com", null]);
        h.Owner.Requests.Count.ShouldBe(1, "one request for the three objects");
        h.Owner.Requests.Single().Ids.OrderBy(x => x).ShouldBe(["c1", "c2", "nobody"]);
    }

    [ServiceBusFact]
    public async Task The_headers_of_the_context_reach_the_owner()
    {
        await using var h = await StartAsync("sbtest2");
        var view = new TransportCustomerView { CustomerId = "c1" };

        await h.MapAsync(view, new TestContext(new Dictionary<string, string> { ["x-tenant"] = "acme" }));

        view.Name.ShouldBe("Alice");
        h.Owner.Requests.Single().Headers["x-tenant"].ShouldBe("acme");
    }

    [ServiceBusFact]
    public async Task An_error_of_the_owner_reaches_the_consumer()
    {
        await using var h = await StartAsync("sbtest3");
        h.Owner.Fail = new InvalidOperationException("the owner is broken");

        var error = await Should.ThrowAsync<Exception>(() =>
            h.MapAsync(new TransportCustomerView { CustomerId = "c1" }));

        error.ToString().ShouldContain("the owner is broken");
    }

    [ServiceBusFact]
    public async Task Without_an_owner_the_request_times_out()
    {
        await using var h = await StartAsync("sbtest4", startOwner: false, timeout: TimeSpan.FromSeconds(3));

        await Should.ThrowAsync<Exception>(() => h.MapAsync(new TransportCustomerView { CustomerId = "c1" }));
    }
}
