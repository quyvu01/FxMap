using FxMap.Fluent;
using FxMap.Helpers;

namespace FxMap.MetadataCache;

internal sealed record EntityConfigMetadata(
    Type ModelType,
    Type DistributedKeyType,
    string DistributedKey,
    string IdPropertyName,
    string DefaultPropertyName,
    IReadOnlyCollection<ExposedNameStore> ExposedNameStores)
{
    public Type GetDistributedKeyType() =>
        DistributedKeyTypeFactory.Resolve(DistributedKeyType, DistributedKey);
}