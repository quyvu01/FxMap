namespace FxMap.Aws.Sqs.Extensions;

internal static class Extensions
{
    internal static string GetQueueName(this Type type) =>
        $"FxMap-{type.Namespace}-{type.Name}".Replace(".", "-").ToLower();
}