using BenchmarkDotNet.Attributes;
using FxMap.Abstractions;

namespace FxMap.Benchmark.EngineOverhead;

/// <summary>The same mapping with a mapper of FxMapBuilder, without a container.</summary>
public class BuilderOverheadBenchmark : EngineOverheadBenchmark
{
    private IDistributedMapper _built = null!;

    public override void Setup()
    {
        base.Setup();
        _built = FxMapBuilder.Create().AddProfiles(typeof(EoOrderViewProfile)).UseRequestClient(new EoClient()).Build();
        _built.MapDataAsync(Views).GetAwaiter().GetResult();
    }

    [Benchmark]
    public Task Builder_Map() => _built.MapDataAsync(Views);

    [Benchmark]
    public IDistributedMapper Builder_Build() =>
        FxMapBuilder.Create().AddProfiles(typeof(EoOrderViewProfile)).UseRequestClient(new EoClient()).Build();
}
