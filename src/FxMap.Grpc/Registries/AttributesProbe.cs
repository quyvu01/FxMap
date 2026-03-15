namespace FxMap.Grpc.Registries;

public sealed record DistributedKeysProbe(bool IsProbed, Type[] DistributedKeyTypes);