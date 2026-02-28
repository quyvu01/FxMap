namespace FxMap.RabbitMq.Extensions;

internal static class Extensions
{
    internal static string GetExchangeName(this Type type) => $"FxMap-{type.Namespace}:{type.Name}";
}