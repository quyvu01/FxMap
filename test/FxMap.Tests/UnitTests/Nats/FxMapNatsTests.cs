using FxMap.Nats;
using FxMap.Nats.Implementations;
using NATS.Net;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Nats;

/// <summary>FxMapNats: the NATS request client of FxMapBuilder (no broker needed: NATS connects on the first request).</summary>
public class FxMapNatsTests
{
    [Fact]
    public async Task Creates_the_request_client_of_the_transport()
    {
        await using var nats = new NatsClient("nats://localhost:4222");

        FxMapNats.CreateRequestClient(nats, topicPrefix: "shop", requestTimeout: TimeSpan.FromSeconds(5))
            .ShouldBeOfType<NatsRequestClient>();
    }

    [Fact]
    public async Task The_arguments_are_checked()
    {
        await using var nats = new NatsClient("nats://localhost:4222");

        Should.Throw<ArgumentNullException>(() => FxMapNats.CreateRequestClient(null));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            FxMapNats.CreateRequestClient(nats, requestTimeout: TimeSpan.FromSeconds(-1)));
    }
}
