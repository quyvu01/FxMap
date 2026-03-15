using FxMap.Azure.ServiceBus.Abstractions;

namespace FxMap.Azure.ServiceBus.Implementations;

internal sealed class AzureServiceBusConfiguration(string topicPrefix, int maxConcurrentSessions)
    : IAzureServiceBusConfiguration
{
    public string TopicPrefix { get; } = topicPrefix;
    public int MaxConcurrentSessions { get; } = maxConcurrentSessions;

    public string GetRequestQueue(Type type) =>
        string.IsNullOrEmpty(TopicPrefix)
            ? $"fxmap-request-{type.Namespace}-{type.Name}".Replace('.', '-').ToLower()
            : $"{TopicPrefix}-fxmap-request-{type.Namespace}-{type.Name}".Replace('.', '-').ToLower();

    public string GetReplyQueue(Type type) =>
        string.IsNullOrEmpty(TopicPrefix)
            ? $"fxmap-reply-{type.Namespace}-{type.Name}".Replace('.', '-').ToLower()
            : $"{TopicPrefix}-fxmap-reply-{type.Namespace}-{type.Name}".Replace('.', '-').ToLower();
}
