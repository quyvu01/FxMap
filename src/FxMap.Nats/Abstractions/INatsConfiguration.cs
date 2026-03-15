namespace FxMap.Nats.Abstractions;

internal interface INatsConfiguration
{
    string TopicPrefix { get; }
    string GetSubject(Type type);
}
