using Amazon;
using FxMap.Abstractions;
using FxMap.Aws.Sqs.Extensions;
using FxMap.Registries;
using Shouldly;
using Xunit;

namespace FxMap.Tests.TransportTests;

/// <summary>
/// The Amazon SQS transport between two services, on an SQS compatible service (set FXMAP_TEST_SQS, e.g. LocalStack at
/// http://localhost:4566). The queue names depend only on the distributed key, so each test uses an account of its own:
/// LocalStack keeps the queues of every 12 digit access key id apart.
/// </summary>
[Collection(TransportTestsCollection.Name)]
public class SqsTransportTests
{
    private static Action<MapConfigurator> Sqs(string account) => cfg =>
        cfg.AddSqs(s => s.Region(RegionEndpoint.USEast1, c =>
        {
            c.ServiceUrl(TransportEnvironment.Get(TransportEnvironment.Sqs));
            c.AccessKeyId(account);
            c.SecretAccessKey("test");
        }));

    private static string NewAccount() => Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString();

    private static Task<TransportHarness> StartAsync(string account, bool startOwner = true, TimeSpan? timeout = null) =>
        // The owner creates its queue when it starts, and the client fails fast when the queue does not exist yet: wait for it.
        TransportHarness.StartAsync(
            r => OwnerHosts.BrokerAsync(r, Sqs(account), startOwner, ready: TimeSpan.FromSeconds(3)), _ => Sqs(account), timeout);

    private sealed class TestContext(Dictionary<string, string> headers) : IContext
    {
        public Dictionary<string, string> Headers { get; } = headers;
        public CancellationToken CancellationToken => CancellationToken.None;
    }

    [SqsFact]
    public async Task The_consumer_is_filled_by_the_owner_through_the_queue()
    {
        await using var h = await StartAsync(NewAccount());
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

    [SqsFact]
    public async Task The_headers_of_the_context_reach_the_owner()
    {
        await using var h = await StartAsync(NewAccount());
        var view = new TransportCustomerView { CustomerId = "c1" };

        await h.MapAsync(view, new TestContext(new Dictionary<string, string> { ["x-tenant"] = "acme" }));

        view.Name.ShouldBe("Alice");
        h.Owner.Requests.Single().Headers["x-tenant"].ShouldBe("acme");
    }

    [SqsFact]
    public async Task An_error_of_the_owner_reaches_the_consumer()
    {
        await using var h = await StartAsync(NewAccount());
        h.Owner.Fail = new InvalidOperationException("the owner is broken");

        var error = await Should.ThrowAsync<Exception>(() =>
            h.MapAsync(new TransportCustomerView { CustomerId = "c1" }));

        error.ToString().ShouldContain("the owner is broken");
    }

    [SqsFact]
    public async Task Without_an_owner_the_request_times_out()
    {
        await using var h = await StartAsync(NewAccount(), startOwner: false, timeout: TimeSpan.FromSeconds(3));

        await Should.ThrowAsync<Exception>(() => h.MapAsync(new TransportCustomerView { CustomerId = "c1" }));
    }
}
