using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace FxMap.Azure.ServiceBus.Wrappers;

/// <summary>
/// The client of the namespace and its administration client. It is disposable so that the container closes the AMQP
/// connection when it is disposed: a record that holds a <see cref="ServiceBusClient"/> without disposing it leaves the
/// connection open (an emulator or a namespace has a quota of connections: <c>ConnectionsQuotaExceeded</c>).
/// </summary>
internal sealed record AzureServiceBusClientWrapper(
    ServiceBusClient ServiceBusClient,
    ServiceBusAdministrationClient ServiceBusAdministrationClient) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => ServiceBusClient.DisposeAsync();
}
