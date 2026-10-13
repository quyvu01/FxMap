using System.Net.Http.Headers;
using System.Text;
using FxMap.Abstractions;
using FxMap.RabbitMq.Extensions;
using FxMap.Registries;
using Shouldly;
using Xunit;

namespace FxMap.Tests.TransportTests;

/// <summary>
/// The RabbitMQ transport between two services (set FXMAP_TEST_RABBITMQ, e.g. localhost). The queue names depend only on
/// the distributed key, so each test gets a virtual host of its own, created and deleted through the management API.
/// </summary>
[Collection(TransportTestsCollection.Name)]
public class RabbitMqTransportTests
{
    internal sealed record Endpoint(string Host, int AmqpPort, int ManagementPort)
    {
        public static Endpoint FromEnvironment()
        {
            var parts = TransportEnvironment.Get(TransportEnvironment.RabbitMq).Split(':');
            return new Endpoint(parts[0], parts.Length > 1 ? int.Parse(parts[1]) : 5672,
                parts.Length > 2 ? int.Parse(parts[2]) : 15672);
        }
    }

    internal sealed class VirtualHost(Endpoint endpoint, string name) : IAsyncDisposable
    {
        public string Name { get; } = name;

        private HttpClient Client()
        {
            var client = new HttpClient { BaseAddress = new Uri($"http://{endpoint.Host}:{endpoint.ManagementPort}/") };
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes("guest:guest")));
            return client;
        }

        public static async Task<VirtualHost> CreateAsync(Endpoint endpoint)
        {
            var host = new VirtualHost(endpoint, $"fxmaptest{Guid.NewGuid():N}");
            using var client = host.Client();
            (await client.PutAsync($"api/vhosts/{host.Name}", null)).EnsureSuccessStatusCode();
            // guest may use the new virtual host
            var permissions = new StringContent("""{"configure":".*","write":".*","read":".*"}""", Encoding.UTF8,
                "application/json");
            (await client.PutAsync($"api/permissions/{host.Name}/guest", permissions)).EnsureSuccessStatusCode();
            return host;
        }

        public async ValueTask DisposeAsync()
        {
            using var client = Client();
            await client.DeleteAsync($"api/vhosts/{Name}");
        }
    }

    private sealed class Run(VirtualHost vhost, TransportHarness harness) : IAsyncDisposable
    {
        public TransportHarness Harness { get; } = harness;

        public async ValueTask DisposeAsync()
        {
            await Harness.DisposeAsync();
            await vhost.DisposeAsync();
        }
    }

    private static Action<MapConfigurator> RabbitMq(Endpoint endpoint, string vhost) => cfg =>
        cfg.AddRabbitMq(c => c.Host(endpoint.Host, vhost, endpoint.AmqpPort));

    private static async Task<Run> StartAsync(bool startOwner = true, TimeSpan? timeout = null)
    {
        var endpoint = Endpoint.FromEnvironment();
        var vhost = await VirtualHost.CreateAsync(endpoint);
        var harness = await TransportHarness.StartAsync(
            r => OwnerHosts.BrokerAsync(r, RabbitMq(endpoint, vhost.Name), startOwner), _ => RabbitMq(endpoint, vhost.Name),
            timeout);
        return new Run(vhost, harness);
    }

    private sealed class TestContext(Dictionary<string, string> headers) : IContext
    {
        public Dictionary<string, string> Headers { get; } = headers;
        public CancellationToken CancellationToken => CancellationToken.None;
    }

    [RabbitMqFact]
    public async Task The_consumer_is_filled_by_the_owner_through_the_broker()
    {
        await using var run = await StartAsync();
        var views = new[]
        {
            new TransportCustomerView { CustomerId = "c1" }, new TransportCustomerView { CustomerId = "c2" },
            new TransportCustomerView { CustomerId = "nobody" }
        };

        await run.Harness.MapAsync(views);

        views.Select(v => v.Name).ShouldBe(["Alice", "Bob", null]);
        views.Select(v => v.Email).ShouldBe(["alice@example.com", "bob@example.com", null]);
        run.Harness.Owner.Requests.Count.ShouldBe(1, "one request for the three objects");
        run.Harness.Owner.Requests.Single().Ids.OrderBy(x => x).ShouldBe(["c1", "c2", "nobody"]);
    }

    [RabbitMqFact]
    public async Task The_headers_of_the_context_reach_the_owner()
    {
        await using var run = await StartAsync();
        var view = new TransportCustomerView { CustomerId = "c1" };

        await run.Harness.MapAsync(view, new TestContext(new Dictionary<string, string> { ["x-tenant"] = "acme" }));

        view.Name.ShouldBe("Alice");
        run.Harness.Owner.Requests.Single().Headers["x-tenant"].ShouldBe("acme");
    }

    [RabbitMqFact]
    public async Task An_error_of_the_owner_reaches_the_consumer()
    {
        await using var run = await StartAsync();
        run.Harness.Owner.Fail = new InvalidOperationException("the owner is broken");

        var error = await Should.ThrowAsync<Exception>(() =>
            run.Harness.MapAsync(new TransportCustomerView { CustomerId = "c1" }));

        error.ToString().ShouldContain("the owner is broken");
    }

    [RabbitMqFact]
    public async Task Without_an_owner_the_request_times_out()
    {
        await using var run = await StartAsync(startOwner: false, timeout: TimeSpan.FromSeconds(2));

        await Should.ThrowAsync<Exception>(() =>
            run.Harness.MapAsync(new TransportCustomerView { CustomerId = "c1" }));
    }

    [RabbitMqFact]
    public async Task The_errors_of_the_expressions_that_the_owner_could_not_build_reach_the_consumer()
    {
        await using var run = await StartAsync();

        await TransportScenarios.Expression_errors_cross_the_wire(run.Harness);
    }

    [RabbitMqFact]
    public async Task Concurrent_requests_each_get_their_own_answer()
    {
        await using var run = await StartAsync();

        await TransportScenarios.Concurrent_requests_each_get_their_own_answer(run.Harness);
    }
}
