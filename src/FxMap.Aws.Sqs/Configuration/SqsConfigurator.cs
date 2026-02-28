using Amazon;
using FxMap.Aws.Sqs.Statics;

namespace FxMap.Aws.Sqs.Configuration;

public sealed class SqsConfigurator
{
    public void Region(RegionEndpoint region, Action<SqsCredential> configure = null)
    {
        SqsStatics.AwsRegion = region;
        var sqsCredential = new SqsCredential();
        configure?.Invoke(sqsCredential);
    }
}
