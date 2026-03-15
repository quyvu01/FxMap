using Amazon;

namespace FxMap.Aws.Sqs.Configuration;

public sealed class SqsConfigurator
{
    internal RegionEndpoint AwsRegionValue { get; private set; }
    internal SqsCredential Credential { get; } = new();

    public void Region(RegionEndpoint region, Action<SqsCredential> configure = null)
    {
        AwsRegionValue = region;
        configure?.Invoke(Credential);
    }
}
