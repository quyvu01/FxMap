using BenchmarkDotNet.Attributes;
using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.Extensions;
using FxMap.Models;
using FxMap.Fluent;
using FxMap.Responses;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Benchmark.EngineOverhead;

public sealed class EoUserKey : IDistributedKey;
public sealed class EoProductKey : IDistributedKey;

public sealed class EoOrderView
{
    public string UserId { get; set; } = "";
    public string UserName { get; set; }
    public string UserEmail { get; set; }
    public string ProductId { get; set; } = "";
    public string ProductName { get; set; }
}

internal sealed class EoOrderViewProfile : ProfileOf<EoOrderView>
{
    protected override void Configure()
    {
        UseDistributedKey<EoUserKey>().Of(x => x.UserId).For(x => x.UserName, "Name").For(x => x.UserEmail, "Email");
        UseDistributedKey<EoProductKey>().Of(x => x.ProductId).For(x => x.ProductName, "Name");
    }
}

/// <summary>Answers in memory, so only the engine (pipelines, mapper, DI) is measured.</summary>
public sealed class EoClient : IRequestClient
{
    public Task<ItemsResponse<DataResponse>> RequestAsync<TDistributedKey>(
        RequestContext<TDistributedKey> requestContext) where TDistributedKey : IDistributedKey
    {
        var q = requestContext.Query;
        return Task.FromResult(new ItemsResponse<DataResponse>([
            ..q.SelectorIds.Select(id => new DataResponse
            {
                Id = id, Values = [..q.Expressions.Select(e => new ValueResponse { Expression = e, Value = "\"v\"" })]
            })
        ]));
    }
}

/// <summary>Engine cost of AddFxMap: resolving the mapper and mapping with an in-memory client.</summary>
[MemoryDiagnoser]
public class EngineOverheadBenchmark
{
    [Params(1, 100)] public int Count { get; set; }

    protected ServiceProvider Provider = null!;
    protected IDistributedMapper Mapper = null!;
    protected List<EoOrderView> Views = null!;
    private AsyncServiceScope _scope;

    [GlobalSetup]
    public virtual void Setup()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestClient>(new EoClient());
        services.AddFxMap(cfg => cfg.AddProfileConfigs(typeof(EoOrderViewProfile)));
        Provider = services.BuildServiceProvider();
        _scope = Provider.CreateAsyncScope();
        Mapper = _scope.ServiceProvider.GetRequiredService<IDistributedMapper>();
        Views = [..Enumerable.Range(0, Count).Select(i => new EoOrderView { UserId = $"u{i % 10}", ProductId = $"p{i % 7}" })];
        Mapper.MapDataAsync(Views).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Provider.Dispose();
    }

    /// <summary>What a request does with AddFxMap: new scope, resolve the mapper, map.</summary>
    [Benchmark(Baseline = true)]
    public async Task Container_ScopeResolveMap()
    {
        await using var scope = Provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDistributedMapper>().MapDataAsync(Views);
    }

    /// <summary>Mapping only, with a mapper resolved once.</summary>
    [Benchmark]
    public Task Container_Map() => Mapper.MapDataAsync(Views);

    /// <summary>Resolving the mapper only (object graph of the transient mapper).</summary>
    [Benchmark]
    public object Container_Resolve()
    {
        using var scope = Provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDistributedMapper>();
    }
}
