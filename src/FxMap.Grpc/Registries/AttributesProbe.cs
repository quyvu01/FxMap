namespace FxMap.Grpc.Registries;

public sealed record DistributedKeysProbe(bool IsProbed, string[] DistributedKeyTypes);