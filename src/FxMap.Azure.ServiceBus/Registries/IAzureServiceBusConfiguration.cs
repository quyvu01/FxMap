namespace FxMap.Azure.ServiceBus.Registries;

internal interface IAzureServiceBusConfiguration
{
    string TopicPrefix { get; }
    int MaxConcurrentSessions { get; }
    string GetRequestQueue(Type type);
    string GetReplyQueue(Type type);
}
