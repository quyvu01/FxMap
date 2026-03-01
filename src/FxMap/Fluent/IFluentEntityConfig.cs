namespace FxMap.Fluent;

public interface IFluentEntityConfig
{
    Type EntityType { get; }
    string IdPropertyName { get; }
    string DefaultPropertyName { get; }
    IReadOnlyCollection<ExposedNameStore> ExposedNameStores { get; }
    Type DistributedKeyType { get; }
    string DistributedKey { get; }
}