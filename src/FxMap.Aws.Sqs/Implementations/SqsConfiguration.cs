using Amazon;
using FxMap.Aws.Sqs.Abstractions;

namespace FxMap.Aws.Sqs.Implementations;

internal sealed class SqsConfiguration(
    string awsAccessKeyId,
    string awsSecretAccessKey,
    RegionEndpoint awsRegion,
    string serviceUrl)
    : ISqsConfiguration
{
    public string AwsAccessKeyId { get; } = awsAccessKeyId;
    public string AwsSecretAccessKey { get; } = awsSecretAccessKey;
    public RegionEndpoint AwsRegion { get; } = awsRegion;
    public string ServiceUrl { get; } = serviceUrl;

    public string GetQueueName(Type type) =>
        $"FxMap-{type.Namespace}-{type.Name}".Replace(".", "-").ToLower();
}
