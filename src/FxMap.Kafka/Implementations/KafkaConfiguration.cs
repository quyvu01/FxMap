using Confluent.Kafka;
using FxMap.Kafka.Abstractions;
using FxMap.Kafka.Registries;

namespace FxMap.Kafka.Implementations;

internal sealed class KafkaConfiguration(string kafkaHost, KafkaSslOptions kafkaSslOptions) : IKafkaConfiguration
{
    public string KafkaHost { get; } = kafkaHost;
    public KafkaSslOptions KafkaSslOptions { get; } = kafkaSslOptions;

    public string GetRequestTopic(Type type) => $"fxmap-request-topic-{type.FullName}".ToLower();

    public void ApplySsl(ClientConfig clientConfig)
    {
        if (KafkaSslOptions is null) return;
        if (KafkaSslOptions.SslSigalgsList != null) clientConfig.SslSigalgsList = KafkaSslOptions.SslSigalgsList;
        if (KafkaSslOptions.SslCipherSuites != null) clientConfig.SslCipherSuites = KafkaSslOptions.SslCipherSuites;
        if (KafkaSslOptions.SslCurvesList != null) clientConfig.SslCurvesList = KafkaSslOptions.SslCurvesList;
        if (KafkaSslOptions.SslKeyLocation != null) clientConfig.SslKeyLocation = KafkaSslOptions.SslKeyLocation;
        if (KafkaSslOptions.SslKeyPassword != null) clientConfig.SslKeyPassword = KafkaSslOptions.SslKeyPassword;
        if (KafkaSslOptions.SslKeyPem != null) clientConfig.SslKeyPem = KafkaSslOptions.SslKeyPem;
        if (KafkaSslOptions.SslCertificateLocation != null) clientConfig.SslCertificateLocation = KafkaSslOptions.SslCertificateLocation;
        if (KafkaSslOptions.SslCertificatePem != null) clientConfig.SslCertificatePem = KafkaSslOptions.SslCertificatePem;
        if (KafkaSslOptions.SslCaLocation != null) clientConfig.SslCaLocation = KafkaSslOptions.SslCaLocation;
        if (KafkaSslOptions.SslCaPem != null) clientConfig.SslCaPem = KafkaSslOptions.SslCaPem;
        if (KafkaSslOptions.SslCaCertificateStores != null) clientConfig.SslCaCertificateStores = KafkaSslOptions.SslCaCertificateStores;
        if (KafkaSslOptions.SslCrlLocation != null) clientConfig.SslCrlLocation = KafkaSslOptions.SslCrlLocation;
        if (KafkaSslOptions.SslKeystoreLocation != null) clientConfig.SslKeystoreLocation = KafkaSslOptions.SslKeystoreLocation;
        if (KafkaSslOptions.SslKeystorePassword != null) clientConfig.SslKeystorePassword = KafkaSslOptions.SslKeystorePassword;
        if (KafkaSslOptions.SslProviders != null) clientConfig.SslProviders = KafkaSslOptions.SslProviders;
        if (KafkaSslOptions.SslEngineLocation != null) clientConfig.SslEngineLocation = KafkaSslOptions.SslEngineLocation;
        if (KafkaSslOptions.SslEngineId != null) clientConfig.SslEngineId = KafkaSslOptions.SslEngineId;
        if (KafkaSslOptions.SslEndpointIdentificationAlgorithm != null)
            clientConfig.SslEndpointIdentificationAlgorithm = KafkaSslOptions.SslEndpointIdentificationAlgorithm;
    }
}
