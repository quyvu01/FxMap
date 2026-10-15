using FxMap.Extensions;

namespace FxMap.Grpc.Registries;

public interface IGrpcClientsConfigurator
{
    /// <summary>
    /// Adds multiple gRPC server hosts.
    /// </summary>
    /// <param name="serviceHost">The first gRPC server host</param>
    /// <param name="serviceHosts">The gRPC server host URLs to add.</param>
    /// <example>
    /// <code>
    /// register.AddGrpcHosts("https://service1", "https://service2");
    /// </code>
    /// </example>
    void AddGrpcHosts(string serviceHost, params string[] serviceHosts);

    /// <summary>
    /// Adds multiple gRPC server hosts.
    /// </summary>
    /// <param name="serviceHosts">The gRPC server host URLs to add.</param>
    /// <example>
    /// <code>
    /// register.AddGrpcHosts("https://service1", "https://service2");
    /// </code>
    /// </example>
    void AddGrpcHosts(IEnumerable<string> serviceHosts);

    /// <summary>
    /// Adds multiple gRPC server hosts.
    /// </summary>
    /// <param name="serviceHosts">The gRPC server host URLs to add.</param>
    /// <example>
    /// <code>
    /// register.AddGrpcHosts("https://service1", "https://service2");
    /// </code>
    /// </example>
    void AddGrpcHosts(params string[] serviceHosts);
}
