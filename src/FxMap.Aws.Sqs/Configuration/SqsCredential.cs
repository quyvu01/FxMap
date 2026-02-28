using FxMap.Aws.Sqs.Statics;

namespace FxMap.Aws.Sqs.Configuration;

public sealed class SqsCredential
{
    public void AccessKeyId(string accessKeyId) => SqsStatics.AwsAccessKeyId = accessKeyId;
    public void SecretAccessKey(string secretAccessKey) => SqsStatics.AwsSecretAccessKey = secretAccessKey;
    public void ServiceUrl(string serviceUrl) => SqsStatics.ServiceUrl = serviceUrl;
}
