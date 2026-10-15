namespace FxMap.Grpc.Registries;

internal sealed record DistributedKeysProbe(bool IsProbed, string[] DistributedKeyTypes);