using Amazon;

namespace FxMap.Aws.Sqs.Configuration;

public interface ISqsConfigurator
{
    void Region(RegionEndpoint region, Action<SqsCredential> configure = null);
}
