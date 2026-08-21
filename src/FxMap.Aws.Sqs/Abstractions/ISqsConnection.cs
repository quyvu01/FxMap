using Amazon.SQS;

namespace FxMap.Aws.Sqs.Abstractions;

/// <summary>
/// Owns the single <see cref="AmazonSQSClient"/> shared by every SQS transport component
/// (request client, server) in this process. <see cref="AmazonSQSClient"/> is thread-safe and
/// pools its own HTTP connections internally, so there is no benefit to each component dialing
/// its own instance.
/// </summary>
internal interface ISqsConnection
{
    AmazonSQSClient Client { get; }
}
