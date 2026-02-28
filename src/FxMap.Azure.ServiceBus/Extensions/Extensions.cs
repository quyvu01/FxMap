using FxMap.Azure.ServiceBus.Statics;

namespace FxMap.Azure.ServiceBus.Extensions;

internal static class Extensions
{
    extension(Type type)
    {
        internal string GetAzureServiceBusRequestQueue() =>
            string.IsNullOrEmpty(AzureServiceBusStatic.TopicPrefix)
                ? $"fxmap-request-{type.Namespace}-{type.Name}".Replace('.', '-').ToLower()
                : $"{AzureServiceBusStatic.TopicPrefix}-fxmap-request-{type.Namespace}-{type.Name}".Replace('.', '-')
                    .ToLower();

        internal string GetAzureServiceBusReplyQueue() =>
            string.IsNullOrEmpty(AzureServiceBusStatic.TopicPrefix)
                ? $"fxmap-reply-{type.Namespace}-{type.Name}".Replace('.', '-').ToLower()
                : $"{AzureServiceBusStatic.TopicPrefix}-fxmap-reply-{type.Namespace}-{type.Name}".Replace('.', '-')
                    .ToLower();
    }
}