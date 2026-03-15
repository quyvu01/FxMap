using FxMap.Nats.Abstractions;

namespace FxMap.Nats.Implementations;

internal sealed class NatsConfiguration(string topicPrefix) : INatsConfiguration
{
    public string TopicPrefix { get; } = topicPrefix;

    public string GetSubject(Type type) =>
        string.IsNullOrEmpty(TopicPrefix)
            ? $"fxmap-{type.Namespace}-{type.Name}".ToLower()
            : $"{TopicPrefix}-fxmap-{type.Namespace}-{type.Name}".ToLower();
}
