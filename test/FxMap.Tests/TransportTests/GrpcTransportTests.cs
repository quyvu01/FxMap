using FxMap.Abstractions;
using FxMap.Grpc.Extensions;
using Shouldly;
using Xunit;

namespace FxMap.Tests.TransportTests;

/// <summary>
/// The gRPC transport between two services. No broker is needed: the owner is a Kestrel server on a free local port,
/// so these tests always run.
/// </summary>
[Collection(TransportTestsCollection.Name)]
public class GrpcTransportTests
{
    private static Task<TransportHarness> StartAsync(TimeSpan? timeout = null) =>
        TransportHarness.StartAsync(OwnerHosts.GrpcAsync, owner => cfg => cfg.AddGrpcClients(c => c.AddGrpcHosts(owner.Address)),
            timeout);

    private sealed class TestContext(Dictionary<string, string> headers) : IContext
    {
        public Dictionary<string, string> Headers { get; } = headers;
        public CancellationToken CancellationToken => CancellationToken.None;
    }

    [Fact]
    public async Task The_consumer_is_filled_by_the_owner_through_grpc()
    {
        await using var h = await StartAsync();
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

    [Fact]
    public async Task The_headers_of_the_context_reach_the_owner()
    {
        await using var h = await StartAsync();
        var view = new TransportCustomerView { CustomerId = "c1" };

        await h.MapAsync(view, new TestContext(new Dictionary<string, string> { ["x-tenant"] = "acme" }));

        view.Name.ShouldBe("Alice");
        h.Owner.Requests.Single().Headers["x-tenant"].ShouldBe("acme");
    }

    [Fact]
    public async Task An_error_of_the_owner_reaches_the_consumer()
    {
        await using var h = await StartAsync();
        h.Owner.Fail = new InvalidOperationException("the owner is broken");

        var error = await Should.ThrowAsync<Exception>(() =>
            h.MapAsync(new TransportCustomerView { CustomerId = "c1" }));

        // gRPC does not send the message of an unhandled exception to the caller unless EnableDetailedErrors is on.
        var rpc = error as global::Grpc.Core.RpcException ?? error.InnerException as global::Grpc.Core.RpcException;
        rpc.ShouldNotBeNull(error.ToString());
        rpc.StatusCode.ShouldBe(global::Grpc.Core.StatusCode.Unknown);
    }

    [Fact]
    public async Task Without_an_owner_the_request_fails()
    {
        // Nothing listens on port 1 of the loopback.
        await using var h = await TransportHarness.StartAsync(_ => Task.FromResult(new OwnerHandle(null, "http://127.0.0.1:1")),
            owner => cfg => cfg.AddGrpcClients(c => c.AddGrpcHosts(owner.Address)));

        await Should.ThrowAsync<Exception>(() => h.MapAsync(new TransportCustomerView { CustomerId = "c1" }));
    }

    [Fact]
    public async Task The_errors_of_the_expressions_that_the_owner_could_not_build_reach_the_consumer()
    {
        await using var h = await StartAsync();

        await TransportScenarios.Expression_errors_cross_the_wire(h);
    }

    [Fact]
    public async Task Concurrent_requests_each_get_their_own_answer()
    {
        await using var h = await StartAsync();

        await TransportScenarios.Concurrent_requests_each_get_their_own_answer(h);
    }
}
