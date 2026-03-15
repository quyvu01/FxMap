using Amazon;

namespace FxMap.Aws.Sqs.Abstractions;

internal interface ISqsConfiguration
{
    string AwsAccessKeyId { get; }
    string AwsSecretAccessKey { get; }
    RegionEndpoint AwsRegion { get; }
    string ServiceUrl { get; }
    string GetQueueName(Type type);
}
