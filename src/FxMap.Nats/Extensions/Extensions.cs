using FxMap.Nats.Statics;

namespace FxMap.Nats.Extensions;

internal static class Extensions
{
    internal static string GetNatsSubject(this Type type) =>
        string.IsNullOrEmpty(NatsStatics.TopicPrefix)
            ? $"fxmap-{type.Namespace}-{type.Name}".ToLower()
            : $"{NatsStatics.TopicPrefix}-fxmap-{type.Namespace}-{type.Name}".ToLower();
}