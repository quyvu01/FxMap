using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FxMap.Abstractions;
using FxMap.Kafka.Extensions;
using FxMap.Registries;
using Shouldly;
using Xunit;

namespace FxMap.Tests.TransportTests;

/// <summary>
/// The Kafka transport between two services (set FXMAP_TEST_KAFKA, e.g. localhost:9092). The topics and the consumer group of
/// the owner depend only on the distributed key, so the FxMap topics are deleted before each test: a request left behind by
/// a previous test (the time-out one) must not reach the owner of the next.
/// </summary>
[Collection(TransportTestsCollection.Name)]
public class KafkaTransportTests
{
    private static string Bootstrap => TransportEnvironment.Get(TransportEnvironment.Kafka);

    private static Action<MapConfigurator> Kafka() => cfg => cfg.AddKafka(c => c.Host(Bootstrap));

    internal static async Task ResetTopicsAsync()
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = Bootstrap }).Build();
        string[] Topics() => [..admin.GetMetadata(TimeSpan.FromSeconds(10)).Topics
            .Select(t => t.Topic).Where(t => t.StartsWith("fxmap-"))];

        var topics = Topics();
        if (topics.Length == 0) return;
        try
        {
            await admin.DeleteTopicsAsync(topics);
        }
        catch (DeleteTopicsException)
        {
            // already gone
        }

        // Deleting is asynchronous: wait until the broker does not list them, or the owner could not create its topic.
        for (var i = 0; i < 40 && Topics().Length > 0; i++) await Task.Delay(250);
    }

    private static async Task<TransportHarness> StartAsync(bool startOwner = true, TimeSpan? timeout = null)
    {
        await ResetTopicsAsync();
        // The owner joins a consumer group: it takes a few seconds before it is assigned its partition.
        return await TransportHarness.StartAsync(
            r => OwnerHosts.BrokerAsync(r, Kafka(), startOwner, ready: TimeSpan.FromSeconds(8)), _ => Kafka(), timeout);
    }

    private sealed class TestContext(Dictionary<string, string> headers) : IContext
    {
        public Dictionary<string, string> Headers { get; } = headers;
        public CancellationToken CancellationToken => CancellationToken.None;
    }

    [KafkaFact]
    public async Task The_consumer_is_filled_by_the_owner_through_the_topic()
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

    [KafkaFact]
    public async Task The_headers_of_the_context_reach_the_owner()
    {
        await using var h = await StartAsync();
        var view = new TransportCustomerView { CustomerId = "c1" };

        await h.MapAsync(view, new TestContext(new Dictionary<string, string> { ["x-tenant"] = "acme" }));

        view.Name.ShouldBe("Alice");
        h.Owner.Requests.Single().Headers["x-tenant"].ShouldBe("acme");
    }

    [KafkaFact]
    public async Task An_error_of_the_owner_reaches_the_consumer()
    {
        await using var h = await StartAsync();
        h.Owner.Fail = new InvalidOperationException("the owner is broken");

        var error = await Should.ThrowAsync<Exception>(() =>
            h.MapAsync(new TransportCustomerView { CustomerId = "c1" }));

        error.ToString().ShouldContain("the owner is broken");
    }

    [KafkaFact]
    public async Task Without_an_owner_the_request_times_out()
    {
        await using var h = await StartAsync(startOwner: false, timeout: TimeSpan.FromSeconds(3));

        await Should.ThrowAsync<Exception>(() => h.MapAsync(new TransportCustomerView { CustomerId = "c1" }));
    }

    [KafkaFact]
    public async Task The_errors_of_the_expressions_that_the_owner_could_not_build_reach_the_consumer()
    {
        await using var h = await StartAsync();

        await TransportScenarios.Expression_errors_cross_the_wire(h);
    }

    [KafkaFact]
    public async Task Concurrent_requests_each_get_their_own_answer()
    {
        await using var h = await StartAsync();

        await TransportScenarios.Concurrent_requests_each_get_their_own_answer(h);
    }

    [KafkaFact]
    public async Task The_request_topic_is_created_with_the_partitions_and_the_replication_that_are_configured()
    {
        await ResetTopicsAsync();
        Action<MapConfigurator> kafka = cfg => cfg.AddKafka(c =>
        {
            c.Host(Bootstrap);
            c.Partitions(3);
            c.ReplicationFactor(1);
        });
        await using var h = await TransportHarness.StartAsync(
            r => OwnerHosts.BrokerAsync(r, kafka, ready: TimeSpan.FromSeconds(8)), _ => kafka);

        // 200 requests over 3 partitions, each with its own answer
        await TransportScenarios.Concurrent_requests_each_get_their_own_answer(h);

        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = Bootstrap }).Build();
        var topics = admin.GetMetadata(TimeSpan.FromSeconds(10)).Topics.Where(t => t.Topic.StartsWith("fxmap-")).ToList();
        var request = topics.Single(t => t.Topic.StartsWith("fxmap-request-topic-"));
        request.Partitions.Count.ShouldBe(3);
        request.Partitions.ShouldAllBe(p => p.Replicas.Length == 1);
        topics.Single(t => t.Topic.StartsWith("fxmap-response-topic-")).Partitions.Count.ShouldBe(1, "one consumer reads the replies");
    }

    [Fact]
    public void The_partitions_and_the_replication_factor_must_be_at_least_one()
    {
        var configurator = new FxMap.Kafka.Registries.KafkaConfigurator();

        Should.Throw<ArgumentOutOfRangeException>(() => configurator.Partitions(0));
        Should.Throw<ArgumentOutOfRangeException>(() => configurator.ReplicationFactor(0));
    }
}
