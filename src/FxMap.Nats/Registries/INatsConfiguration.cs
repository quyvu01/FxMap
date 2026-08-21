namespace FxMap.Nats.Registries;

internal interface INatsConfiguration
{
    string TopicPrefix { get; }
    string GetSubject(Type type);
}