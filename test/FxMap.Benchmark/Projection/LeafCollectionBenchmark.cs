using BenchmarkDotNet.Attributes;
using FxMap.Abstractions;
using FxMap.Benchmark.Attributes;
using FxMap.Benchmark.FxMapBenchmarks.Handlers;
using FxMap.Extensions;
using FxMap.Fluent;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Benchmark.Projection;

public class LeafBlobDto
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public byte[] Payload { get; set; }
    public List<string> Tags { get; set; }
    public Dictionary<string, string> Labels { get; set; }
    public int[] Numbers { get; set; }
    public Uri Link { get; set; }
}

// Internal: other benchmarks scan exported types only, so this profile stays private to this benchmark.
internal sealed class LeafBlobDtoProfile : ProfileOf<LeafBlobDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserOfAttribute>().Of(x => x.UserId).For(x => x.UserName, "Name");
}

/// <summary>
/// DTOs that carry "not primitive but pointless to walk" values (byte[], List&lt;string&gt;, Dictionary&lt;string,string&gt;,
/// int[], Uri). Compares mapping the same DTOs with and without those values to see what walking them costs.
/// </summary>
[MemoryDiagnoser]
public class LeafCollectionBenchmark
{
    [Params(1_000, 100_000, 1_000_000)] public int PayloadBytes { get; set; }

    private const int DtoCount = 4;

    private ServiceProvider _provider = null!;
    private List<LeafBlobDto> _withLeaves = null!;
    private List<LeafBlobDto> _withoutLeaves = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddTransient<IClientRequestHandler<UserOfAttribute>, UserOfHandler>();
        services.AddFxMap(cfg => cfg.AddProfileConfigs(typeof(LeafBlobDtoProfile)));
        _provider = services.BuildServiceProvider();

        _withoutLeaves = Enumerable.Range(0, DtoCount).Select(i => new LeafBlobDto { UserId = $"u{i}" }).ToList();
        _withLeaves = Enumerable.Range(0, DtoCount).Select(i => new LeafBlobDto
        {
            UserId = $"u{i}",
            Payload = new byte[PayloadBytes],
            Tags = Enumerable.Range(0, PayloadBytes / 100).Select(n => $"tag-{n}").ToList(),
            Labels = Enumerable.Range(0, PayloadBytes / 200).ToDictionary(n => $"k{n}", n => $"v{n}"),
            Numbers = new int[PayloadBytes / 4],
            Link = new Uri("https://example.com/a/b?q=1")
        }).ToList();

        // Guard against measuring a mapper that silently does nothing.
        foreach (var dtos in new[] { _withLeaves, _withoutLeaves })
        {
            _provider.GetRequiredService<IDistributedMapper>().MapDataAsync(dtos).GetAwaiter().GetResult();
            if (dtos.Any(d => string.IsNullOrEmpty(d.UserName)))
                throw new InvalidOperationException("MapDataAsync did not map UserName");
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();

    /// <summary>Same DTOs without any leaf collection: the floor.</summary>
    [Benchmark(Baseline = true)]
    public Task Map_WithoutLeafCollections() =>
        _provider.GetRequiredService<IDistributedMapper>().MapDataAsync(_withoutLeaves);

    [Benchmark]
    public Task Map_WithLeafCollections() =>
        _provider.GetRequiredService<IDistributedMapper>().MapDataAsync(_withLeaves);
}
