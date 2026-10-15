using Azure.Messaging.ServiceBus;

namespace FxMap.Azure.ServiceBus.Configuration;

public interface IAzureServiceBusConfigurator
{
    void Host(string connectionString, Action<ServiceBusClientOptions> serviceBusClientOptions = null);

    void TopicPrefix(string topicPrefix);

    void MaxConcurrentSessions(int maxConcurrentSessions);
}
