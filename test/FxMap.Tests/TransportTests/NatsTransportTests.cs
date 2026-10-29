using FxMap.Abstractions;
using FxMap.Nats;
using FxMap.Nats.Extensions;
using FxMap.Registries;
using NATS.Net;
using Shouldly;
using Xunit;

namespace FxMap.Tests.TransportTests;

/// <summary>The NATS transport between two services (set FXMAP_TEST_NATS, e.g. nats://localhost:4222).</summary>
[Collection(TransportTestsCollection.Name)]
public class NatsTransportTests
{
    private static Action<IMapConfigurator> Nats(string prefix) => cfg =>
        cfg.AddNats(c =>
        {
            c.TopicPrefix(prefix);
            c.NatsOpts(o => o.Url = TransportEnvironment.Get(TransportEnvironment.Nats));
        });

    // A prefix per test: nothing else on the broker (or a previous run) can answer.
    private static string NewPrefix() => $"fxmaptest{Guid.NewGuid():N}";

    private static Task<TransportHarness> StartAsync(string prefix, bool startOwner = true, TimeSpan? timeout = null) =>
        TransportHarness.StartAsync(r => OwnerHosts.BrokerAsync(r, Nats(prefix), startOwner), _ => Nats(prefix), timeout);

    private sealed class TestContext(Dictionary<string, string> headers) : IContext
    {
        public Dictionary<string, string> Headers { get; } = headers;
        public CancellationToken CancellationToken => CancellationToken.None;
    }

    [NatsFact]
    public async Task The_consumer_is_filled_by_the_owner_through_the_broker()
    {
        var prefix = NewPrefix();
        await using var h = await StartAsync(prefix);
        var views = new[]
        {
            new TransportCustomerView { CustomerId = "c1" },
            new TransportCustomerView { CustomerId = "c2" },
            new TransportCustomerView { CustomerId = "nobody" }
        };

        await h.MapAsync(views);

        views.Select(v => v.Name).ShouldBe(["Alice", "Bob", null]);
        h.Owner.Requests.Count.ShouldBe(1, "one request for the three objects");
        h.Owner.Requests.Single().Ids.OrderBy(x => x).ShouldBe(["c1", "c2", "nobody"]);
    }

    [NatsFact]
    public async Task The_headers_of_the_context_reach_the_owner()
    {
        var prefix = NewPrefix();
        await using var h = await StartAsync(prefix);
        var view = new TransportCustomerView { CustomerId = "c1" };

        await h.MapAsync(view, new TestContext(new Dictionary<string, string> { ["x-tenant"] = "acme" }));

        view.Name.ShouldBe("Alice");
        h.Owner.Requests.Single().Headers["x-tenant"].ShouldBe("acme");
    }

    [NatsFact]
    public async Task An_error_of_the_owner_reaches_the_consumer()
    {
        var prefix = NewPrefix();
        await using var h = await StartAsync(prefix);
        h.Owner.Fail = new InvalidOperationException("the owner is broken");

        var error = await Should.ThrowAsync<Exception>(() =>
            h.MapAsync(new TransportCustomerView { CustomerId = "c1" }));

        error.ToString().ShouldContain("the owner is broken");
    }

    [NatsFact]
    public async Task Without_an_owner_the_request_times_out()
    {
        var prefix = NewPrefix();
        await using var h = await StartAsync(prefix, startOwner: false, timeout: TimeSpan.FromSeconds(2));

        await Should.ThrowAsync<Exception>(() =>
            h.MapAsync(new TransportCustomerView { CustomerId = "c1" }));
    }

    [NatsFact]
    public async Task The_errors_of_the_expressions_that_the_owner_could_not_build_reach_the_consumer()
    {
        await using var h = await StartAsync(NewPrefix());

        await TransportScenarios.Expression_errors_cross_the_wire(h);
    }

    [NatsFact]
    public async Task Concurrent_requests_each_get_their_own_answer()
    {
        await using var h = await StartAsync(NewPrefix());

        await TransportScenarios.Concurrent_requests_each_get_their_own_answer(h);
    }

    // FxMapBuilder on the consumer side, AddFxMap + AddNats on the owner side: the same subjects, the same wire.
    private static IDistributedMapper BuilderConsumer(NatsClient nats, string prefix, TimeSpan? timeout = null) =>
        FxMapBuilder.Create()
            .AddProfiles(typeof(TransportCustomerViewProfile))
            .UseRequestClient(FxMapNats.CreateRequestClient(nats, prefix, timeout))
            .ThrowIfException()
            .Build();

    [NatsFact]
    public async Task A_consumer_built_without_a_container_is_filled_by_the_owner_through_the_broker()
    {
        var prefix = NewPrefix();
        var recorder = new OwnerRecorder();
        await using var owner = await OwnerHosts.BrokerAsync(recorder, Nats(prefix));
        await using var nats = new NatsClient(TransportEnvironment.Get(TransportEnvironment.Nats));
        var views = new[]
        {
            new TransportCustomerView { CustomerId = "c1" },
            new TransportCustomerView { CustomerId = "c2" },
            new TransportCustomerView { CustomerId = "nobody" }
        };

        await BuilderConsumer(nats, prefix).MapDataAsync(views,
            new TestContext(new Dictionary<string, string> { ["x-tenant"] = "acme" }));

        views.Select(v => v.Name).ShouldBe(["Alice", "Bob", null]);
        views[0].Email.ShouldNotBeNullOrEmpty();
        var request = recorder.Requests.ShouldHaveSingleItem();
        request.Ids.OrderBy(x => x).ShouldBe(["c1", "c2", "nobody"]);
        request.Headers["x-tenant"].ShouldBe("acme");
    }

    [NatsFact]
    public async Task A_consumer_built_without_a_container_gets_the_error_of_the_owner()
    {
        var prefix = NewPrefix();
        var recorder = new OwnerRecorder { Fail = new InvalidOperationException("the owner is broken") };
        await using var owner = await OwnerHosts.BrokerAsync(recorder, Nats(prefix));
        await using var nats = new NatsClient(TransportEnvironment.Get(TransportEnvironment.Nats));

        var error = await Should.ThrowAsync<Exception>(() =>
            BuilderConsumer(nats, prefix).MapDataAsync(new TransportCustomerView { CustomerId = "c1" }));

        error.ToString().ShouldContain("the owner is broken");
    }

    [NatsFact]
    public async Task A_consumer_built_without_a_container_times_out_without_an_owner()
    {
        await using var nats = new NatsClient(TransportEnvironment.Get(TransportEnvironment.Nats));

        await Should.ThrowAsync<Exception>(() => BuilderConsumer(nats, NewPrefix(), TimeSpan.FromSeconds(1))
            .MapDataAsync(new TransportCustomerView { CustomerId = "c1" }));
    }
}
