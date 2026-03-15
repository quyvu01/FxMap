namespace FxMap.Azure.ServiceBus.Abstractions;

internal interface IAzureServiceBusConfiguration
{
    string TopicPrefix { get; }
    int MaxConcurrentSessions { get; }
    string GetRequestQueue(Type type);
    string GetReplyQueue(Type type);
}
