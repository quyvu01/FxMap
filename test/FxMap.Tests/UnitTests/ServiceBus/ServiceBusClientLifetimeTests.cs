using FxMap.Azure.ServiceBus.Extensions;
using FxMap.Azure.ServiceBus.Wrappers;
using FxMap.Extensions;
using FxMap.Tests.ProviderTests;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.ServiceBus;

/// <summary>The Service Bus client opens an AMQP connection: it has to be closed when the container is disposed.</summary>
public class ServiceBusClientLifetimeTests
{
    [Fact]
    public async Task Disposing_the_container_closes_the_client_of_the_namespace()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxMap(cfg =>
        {
            cfg.AddEntityConfigs(typeof(ProviderCustomerConfig));
            // Nothing is sent: creating the client does not connect.
            cfg.AddAzureServiceBus(s =>
                s.Host("Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=key;SharedAccessKey=secret"));
        });
        var provider = services.BuildServiceProvider();
        var wrapper = provider.GetRequiredService<AzureServiceBusClientWrapper>();
        wrapper.ServiceBusClient.IsClosed.ShouldBeFalse();

        await provider.DisposeAsync();

        wrapper.ServiceBusClient.IsClosed.ShouldBeTrue();
    }
}
