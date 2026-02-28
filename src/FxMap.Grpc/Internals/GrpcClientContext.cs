using FxMap.Abstractions;

namespace FxMap.Grpc.Internals;

internal sealed record GrpcClientContext(
    Dictionary<string, string> Headers,
    CancellationToken CancellationToken = default)
    : IContext;