using MediatR;

namespace FxMap.Benchmark.FxMapBenchmarks.MediatR;

public sealed record GetUserRequest : IRequest<string>;