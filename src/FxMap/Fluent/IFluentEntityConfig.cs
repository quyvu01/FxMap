namespace FxMap.Fluent;

public interface IFluentEntityConfig
{
    Type ModelType { get; }
    string IdPropertyName { get; }
    string DefaultPropertyName { get; }
    IReadOnlyCollection<ExposedNameStore> ExposedNameStores { get; }
    Type DistributedKeyType { get; }
    string DistributedKey { get; }
}