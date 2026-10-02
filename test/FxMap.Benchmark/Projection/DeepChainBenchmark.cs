using System.Reflection;
using BenchmarkDotNet.Attributes;
using FxMap.Abstractions;
using FxMap.Benchmark.Attributes;
using FxMap.Benchmark.FxMapBenchmarks.Handlers;
using FxMap.Extensions;
using FxMap.Fluent;
using FxMap.Implementations;
using FxMap.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Benchmark.Projection;

public class ChainNode
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public ChainNode Next { get; set; }
}

internal sealed class ChainNodeProfile : ProfileOf<ChainNode>
{
    protected override void Configure() =>
        UseDistributedKey<UserOfAttribute>().Of(x => x.UserId).For(x => x.UserName, "Name");
}

/// <summary>
/// A linked chain of N nodes (one mapping rule per node). Walking it recursively with nested iterators makes every
/// descriptor pass through as many iterator levels as its depth; an explicit stack does not.
/// </summary>
[MemoryDiagnoser]
public class DeepChainBenchmark
{
    [Params(500, 1000, 2000, 4000)] public int Depth { get; set; }

    private ServiceProvider _provider = null!;
    private ChainNode _head = null!;
    private DistributedMapper _mapper = null!;
    private MethodInfo _discover = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddTransient<IClientRequestHandler<UserOfAttribute>, UserOfHandler>();
        services.AddFxMap(cfg => cfg.AddProfileConfigs(typeof(ChainNodeProfile)));
        _provider = services.BuildServiceProvider();
        _mapper = new DistributedMapper(_provider);
        _discover = typeof(DistributedMapper)
            .GetMethod("DiscoverResolvableProperties", BindingFlags.NonPublic | BindingFlags.Instance)!;

        _head = new ChainNode { UserId = "u0" };
        var tail = _head;
        for (var i = 1; i < Depth; i++) tail = tail.Next = new ChainNode { UserId = $"u{i % 20}" };

        var count = Discover();
        if (count != Depth) throw new InvalidOperationException($"expected {Depth} descriptors, got {count}");
    }

    [GlobalCleanup]
    public void Cleanup() => _provider.Dispose();

    private int Discover() => ((IEnumerable<PropertyDescriptor>)_discover.Invoke(_mapper, [_head])!).Count();

    /// <summary>Only the graph walk.</summary>
    [Benchmark]
    public int Discover_Chain() => Discover();

    /// <summary>The whole MapDataAsync on the chain.</summary>
    [Benchmark]
    public Task MapDataAsync_Chain() => _mapper.MapDataAsync(_head);
}
