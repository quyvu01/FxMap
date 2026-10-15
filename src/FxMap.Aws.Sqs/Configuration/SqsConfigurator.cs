using Amazon;

namespace FxMap.Aws.Sqs.Configuration;

internal sealed class SqsConfigurator : ISqsConfigurator
{
    internal RegionEndpoint AwsRegionValue { get; private set; }
    internal SqsCredential Credential { get; } = new();

    public void Region(RegionEndpoint region, Action<SqsCredential> configure = null)
    {
        AwsRegionValue = region;
        configure?.Invoke(Credential);
    }
}
