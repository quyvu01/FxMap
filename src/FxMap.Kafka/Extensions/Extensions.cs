namespace FxMap.Kafka.Extensions;

internal static class Extensions
{
    internal static string RequestTopic(this Type type) => $"fxmap-request-topic-{type.FullName}".ToLower();
}