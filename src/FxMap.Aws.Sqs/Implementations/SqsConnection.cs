using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using FxMap.Aws.Sqs.Abstractions;
using FxMap.Aws.Sqs.Registries;

namespace FxMap.Aws.Sqs.Implementations;

/// <summary>
/// Single shared <see cref="AmazonSQSClient"/>, built once and reused by every SQS transport
/// component. Registered as a singleton so <see cref="SqsRequestClient"/> and
/// <see cref="SqsServer"/> share one client instead of each dialing their own.
/// </summary>
internal sealed class SqsConnection : ISqsConnection, IDisposable
{
    public AmazonSQSClient Client { get; }

    public SqsConnection(ISqsConfiguration sqsConfiguration)
    {
        AWSCredentials credentials = null;
        if (!string.IsNullOrEmpty(sqsConfiguration.AwsAccessKeyId) &&
            !string.IsNullOrEmpty(sqsConfiguration.AwsSecretAccessKey))
        {
            credentials =
                new BasicAWSCredentials(sqsConfiguration.AwsAccessKeyId, sqsConfiguration.AwsSecretAccessKey);
        }

        var config = new AmazonSQSConfig
        {
            RegionEndpoint = sqsConfiguration.AwsRegion ?? RegionEndpoint.USEast1
        };

        // Support LocalStack for testing
        if (!string.IsNullOrEmpty(sqsConfiguration.ServiceUrl))
            config.ServiceURL = sqsConfiguration.ServiceUrl;

        Client = credentials != null
            ? new AmazonSQSClient(credentials, config)
            : new AmazonSQSClient(config);
    }

    public void Dispose() => Client.Dispose();
}
