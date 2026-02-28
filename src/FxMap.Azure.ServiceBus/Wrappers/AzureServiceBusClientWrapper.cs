using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace FxMap.Azure.ServiceBus.Wrappers;

internal record AzureServiceBusClientWrapper(
    ServiceBusClient ServiceBusClient,
    ServiceBusAdministrationClient ServiceBusAdministrationClient);