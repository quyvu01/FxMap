using Amazon;
using FxMap.Aws.Sqs.Extensions;
using FxMap.Azure.ServiceBus.Extensions;
using FxMap.Grpc.Extensions;
using FxMap.Kafka.Extensions;
using FxMap.Nats.Extensions;
using FxMap.RabbitMq.Extensions;
using FxMap.Registries;
using Microsoft.Extensions.Configuration;

namespace Shared.Transports;

/// <summary>
/// Registers the transport that the sample services use, read from the configuration: the launch profile sets
/// <c>ASPNETCORE_ENVIRONMENT</c> (Grpc, Nats, RabbitMq, Kafka, Sqs or ServiceBus) and the matching
/// <c>appsettings.{Environment}.json</c> holds the section below. For example:
/// <code>
/// "FxMap": {
///   "Transport": "Nats",
///   "Nats": { "Url": "nats://localhost:4222" }
/// }
/// </code>
/// </summary>
public static class ConfiguredTransportExtensions
{
    public static void AddConfiguredTransport(this IMapConfigurator cfg, IConfiguration configuration)
    {
        var fxMap = configuration.GetSection("FxMap");
        var transport = fxMap["Transport"];
        switch (transport?.ToLowerInvariant())
        {
            case "grpc":
                // A service that only answers (it maps FxMapperGrpc) has no hosts to call.
                var hosts = fxMap.GetSection("Grpc:Hosts").GetChildren().Select(c => c.Value).Where(v => v is not null)
                    .ToArray();
                if (hosts.Length > 0) cfg.AddGrpcClients(c => c.AddGrpcHosts(hosts[0], hosts[1..]));
                break;
            case "nats":
                cfg.AddNats(c => c.NatsOpts(o => o.Url = fxMap["Nats:Url"] ?? "nats://localhost:4222"));
                break;
            case "rabbitmq":
                var rabbit = fxMap.GetSection("RabbitMq");
                cfg.AddRabbitMq(c => c.Host(rabbit["Host"] ?? "localhost", rabbit["VirtualHost"] ?? "fx-map",
                    int.TryParse(rabbit["Port"], out var port) ? port : 5672, credential =>
                    {
                        if (rabbit["UserName"] is { } user) credential.UserName(user);
                        if (rabbit["Password"] is { } password) credential.Password(password);
                    }));
                break;
            case "kafka":
                cfg.AddKafka(c => c.Host(fxMap["Kafka:Host"] ?? "localhost:9092"));
                break;
            case "sqs":
                var sqs = fxMap.GetSection("Sqs");
                cfg.AddSqs(s => s.Region(RegionEndpoint.GetBySystemName(sqs["Region"] ?? "us-east-1"), credential =>
                {
                    if (sqs["ServiceUrl"] is { } serviceUrl) credential.ServiceUrl(serviceUrl);
                    credential.AccessKeyId(sqs["AccessKeyId"] ?? "test");
                    credential.SecretAccessKey(sqs["SecretAccessKey"] ?? "test");
                }));
                break;
            case "servicebus":
                var serviceBus = fxMap.GetSection("ServiceBus");
                var connectionString = serviceBus["ConnectionString"];
                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new InvalidOperationException(
                        "FxMap:ServiceBus:ConnectionString is empty: set it (user secrets or an environment variable " +
                        "FxMap__ServiceBus__ConnectionString) to the connection string of an Azure Service Bus namespace.");
                cfg.AddAzureServiceBus(s =>
                {
                    s.Host(connectionString);
                    if (serviceBus["TopicPrefix"] is { Length: > 0 } prefix) s.TopicPrefix(prefix);
                });
                break;
            default:
                throw new InvalidOperationException(
                    $"FxMap:Transport is '{transport}': use Grpc, Nats, RabbitMq, Kafka, Sqs or ServiceBus " +
                    "(the launch profile sets ASPNETCORE_ENVIRONMENT, which selects appsettings.{Environment}.json).");
        }
    }
}
