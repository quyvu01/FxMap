using FxMap.Abstractions.Transporting;
using FxMap.Implementations;
using FxMap.Nats.Implementations;
using FxMap.Nats.Registries;
using FxMap.Nats.Wrappers;
using NATS.Net;

namespace FxMap.Nats;

/// <summary>The NATS transport for <see cref="FxMapBuilder"/>, without a dependency injection container.</summary>
public static class FxMapNats
{
    /// <summary>
    /// A request client on <paramref name="natsClient"/>, which the caller owns and disposes. It sends on the subjects of
    /// <c>AddNats</c>: give it the topic prefix of the services that own the keys. The client must serialize as the one
    /// <c>AddNats</c> makes (the default serializer registry of NATS.Net).
    /// </summary>
    /// <param name="natsClient">The connection to NATS.</param>
    /// <param name="topicPrefix">The prefix of the subjects, as <c>TopicPrefix</c> of <c>AddNats</c>.</param>
    /// <param name="requestTimeout">How long a reply is waited for; 30 seconds by default.</param>
    public static IRequestClient CreateRequestClient(NatsClient natsClient, string topicPrefix = null,
        TimeSpan? requestTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(natsClient);
        var timeout = requestTimeout ?? TimeSpan.FromSeconds(30);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero, nameof(requestTimeout));
        // The request client reads only the timeout of the configuration.
        var configuration = new MapperConfiguration([], [], new Dictionary<Type, Type>(), 128, 128, false, timeout,
            null, null);
        return new NatsRequestClient(new NatsClientWrapper(natsClient), configuration,
            new NatsConfiguration(topicPrefix));
    }
}
