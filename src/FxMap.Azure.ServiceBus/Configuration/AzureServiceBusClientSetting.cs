using Azure.Messaging.ServiceBus;

namespace FxMap.Azure.ServiceBus.Configuration;

public sealed class AzureServiceBusClientSetting
{
    public string ConnectionString { get; private set; }
    public ServiceBusClientOptions ServiceBusClientOptions { get; private set; }
    internal string TopicPrefixValue { get; private set; }
    internal int MaxConcurrentSessionsValue { get; private set; } = 8;

    public void Host(string connectionString, Action<ServiceBusClientOptions> serviceBusClientOptions = null)
    {
        ConnectionString = connectionString;
        if (serviceBusClientOptions == null) return;
        var options = new ServiceBusClientOptions();
        serviceBusClientOptions.Invoke(options);
        ServiceBusClientOptions = options;
    }

    public void TopicPrefix(string topicPrefix) => TopicPrefixValue = topicPrefix;

    public void MaxConcurrentSessions(int maxConcurrentSessions)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentSessions, 1);
        MaxConcurrentSessionsValue = maxConcurrentSessions;
    }
}
