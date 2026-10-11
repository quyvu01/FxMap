using System.Collections.Concurrent;
using System.Net;
using FxMap.Abstractions;
using FxMap.Extensions;
using FxMap.Grpc.Extensions;
using FxMap.Fluent;
using FxMap.Registries;
using FxMap.Responses;
using FxMap.Tests.ProviderTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FxMap.Tests.TransportTests;

/// <summary>Names of the environment variables that point the transport tests to a real broker.</summary>
public static class TransportEnvironment
{
    public const string Nats = "FXMAP_TEST_NATS";

    /// <summary><c>host[:amqpPort[:managementPort]]</c>, user and password are guest; the management plugin creates the virtual hosts.</summary>
    public const string RabbitMq = "FXMAP_TEST_RABBITMQ";

    /// <summary>The endpoint of an SQS compatible service, e.g. LocalStack: <c>http://localhost:4566</c>.</summary>
    public const string Sqs = "FXMAP_TEST_SQS";

    public static string Get(string variable) => Environment.GetEnvironmentVariable(variable);

    internal static string SkipReason(string variable) => string.IsNullOrWhiteSpace(Get(variable))
        ? $"Set {variable} to run the tests of this transport against a real broker."
        : null;
}

public sealed class NatsFactAttribute : FactAttribute
{
    public NatsFactAttribute() => Skip = TransportEnvironment.SkipReason(TransportEnvironment.Nats);
}

public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute() => Skip = TransportEnvironment.SkipReason(TransportEnvironment.RabbitMq);
}

public sealed class SqsFactAttribute : FactAttribute
{
    public SqsFactAttribute() => Skip = TransportEnvironment.SkipReason(TransportEnvironment.Sqs);
}

/// <summary>The transport tests share static state (the owner's recorder) and a broker: one after the other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TransportTestsCollection
{
    public const string Name = "Transport tests";
}

/// <summary>
/// What the owner service saw, and how it answers. The owner answers from memory: the data providers have their own
/// tests, here only the transport between the two services is under test.
/// </summary>
public sealed class OwnerRecorder
{
    public ConcurrentQueue<(string[] Ids, string[] Expressions, Dictionary<string, string> Headers)> Requests { get; } = new();

    /// <summary>When set, the owner throws this instead of answering.</summary>
    public Exception Fail { get; set; }
}

/// <summary>What the consumer shows of a customer; its key is owned by the other service.</summary>
public class TransportCustomerView
{
    public string CustomerId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

public sealed class TransportCustomerViewProfile : ProfileOf<TransportCustomerView>
{
    protected override void Configure() =>
        UseDistributedKey<ProviderCustomerKey>().Of(x => x.CustomerId).For(x => x.Name).For(x => x.Email, "Email");
}

/// <summary>The owner of the customers: answers "Name" (default property) and "Email" for c1 and c2 only.</summary>
public sealed class OwnerCustomerHandler(OwnerRecorder recorder)
    : IQueryOfHandler<ProviderCustomer, ProviderCustomerKey>
{
    public Task<ItemsResponse<DataResponse>> GetDataAsync(RequestContext<ProviderCustomerKey> context)
    {
        recorder.Requests.Enqueue((context.Query.SelectorIds, context.Query.Expressions, context.Headers));
        if (recorder.Fail is not null) throw recorder.Fail;

        var customers = ProviderData.Customers().ToDictionary(c => c.Id);
        var rows = context.Query.SelectorIds
            .Where(id => id is "c1" or "c2" && customers.ContainsKey(id))
            .Select(id => new DataResponse
            {
                Id = id,
                Values =
                [
                    ..context.Query.Expressions.Select(e => new ValueResponse
                    {
                        Expression = e,
                        Value = Serializable.JsonSerializer.SerializeObject(e == "Email" ? customers[id].Email : customers[id].Name)
                    })
                ]
            });
        return Task.FromResult(new ItemsResponse<DataResponse>([..rows]));
    }
}

/// <summary>A started owner service: what to dispose, and where a consumer reaches it when the transport needs an address.</summary>
public sealed class OwnerHandle(IAsyncDisposable owner, string address = null) : IAsyncDisposable
{
    public string Address { get; } = address;

    public ValueTask DisposeAsync() => owner?.DisposeAsync() ?? ValueTask.CompletedTask;
}

/// <summary>How the owner service of a test is hosted.</summary>
public static class OwnerHosts
{
    private sealed class Stoppable(IHost host) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await host.StopAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                host.Dispose();
            }
        }
    }

    /// <summary>
    /// A generic host: the transport registers a hosted worker that listens on the broker once the host is started.
    /// With <paramref name="start"/> false nobody listens (a request has no owner to answer it).
    /// </summary>
    public static async Task<OwnerHandle> BrokerAsync(OwnerRecorder recorder, Action<MapConfigurator> configureTransport,
        bool start = true)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(recorder);
        builder.Services.AddFxMap(cfg =>
        {
            cfg.AddEntityConfigs(typeof(ProviderCustomerConfig));
            configureTransport(cfg);
            cfg.ThrowIfException();
        });
        builder.Services.AddTransient<IQueryOfHandler<ProviderCustomer, ProviderCustomerKey>, OwnerCustomerHandler>();
        var host = builder.Build();
        if (start)
        {
            await host.StartAsync();
            // The subscription of the owner is made by its worker after the host started: give it a moment.
            await Task.Delay(500);
        }

        return new OwnerHandle(new Stoppable(host));
    }

    /// <summary>A web server on a free local port that serves the FxMap gRPC service (HTTP/2 without TLS).</summary>
    public static async Task<OwnerHandle> GrpcAsync(OwnerRecorder recorder)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0, l => l.Protocols = HttpProtocols.Http2));
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(recorder);
        builder.Services.AddFxMap(cfg =>
        {
            cfg.AddEntityConfigs(typeof(ProviderCustomerConfig));
            cfg.ThrowIfException();
        });
        builder.Services.AddTransient<IQueryOfHandler<ProviderCustomer, ProviderCustomerKey>, OwnerCustomerHandler>();
        var app = builder.Build();
        app.MapFxMapperGrpc();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new OwnerHandle(new Stoppable(app), address);
    }
}

/// <summary>
/// Two services in one process that only talk through a real transport: the owner (entity, answers from memory) and the
/// consumer (profile only, no entity: every request leaves the process).
/// </summary>
public sealed class TransportHarness : IAsyncDisposable
{
    private readonly OwnerHandle _owner;
    private readonly ServiceProvider _consumer;

    public OwnerRecorder Owner { get; }

    private TransportHarness(OwnerHandle owner, ServiceProvider consumer, OwnerRecorder recorder)
    {
        _owner = owner;
        _consumer = consumer;
        Owner = recorder;
    }

    /// <param name="startOwner">Starts the owner service.</param>
    /// <param name="consumerTransport">Registers the transport of the consumer (it may need the address of the owner).</param>
    /// <param name="timeout">The request timeout of the consumer, or the default.</param>
    public static async Task<TransportHarness> StartAsync(Func<OwnerRecorder, Task<OwnerHandle>> startOwner,
        Func<OwnerHandle, Action<MapConfigurator>> consumerTransport, TimeSpan? timeout = null)
    {
        var recorder = new OwnerRecorder();
        var owner = await startOwner(recorder);

        var consumerServices = new ServiceCollection();
        consumerServices.AddLogging(); // a host has it; some transports ask for an ILogger<T>
        consumerServices.AddFxMap(cfg =>
        {
            cfg.AddProfileConfigs(typeof(TransportCustomerViewProfile));
            consumerTransport(owner)(cfg);
            if (timeout is { } t) cfg.SetRequestTimeOut(t);
            cfg.ThrowIfException();
        });
        return new TransportHarness(owner, consumerServices.BuildServiceProvider(), recorder);
    }

    public async Task MapAsync(object value, IContext context = null)
    {
        await using var scope = _consumer.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDistributedMapper>().MapDataAsync(value, context);
    }

    public async ValueTask DisposeAsync()
    {
        await _consumer.DisposeAsync();
        await _owner.DisposeAsync();
    }
}
