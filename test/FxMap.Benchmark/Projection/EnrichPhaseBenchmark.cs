using System.Reflection;
using BenchmarkDotNet.Attributes;
using FxMap.Abstractions;
using FxMap.Implementations;
using FxMap.Models;
using FxMap.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RequestContext = FxMap.PublicContracts.RequestContext;

namespace FxMap.Benchmark.Projection;

/// <summary>
/// Splits the cost of MapDataAsync into phases, using the Customer key (order 0) as the representative fetch.
/// Each row is measured independently, so rows overlap (Fetch contains Handler contains EF); read them as a nesting:
/// Full > Fetch > Handler > EfDirect.
/// </summary>
[MemoryDiagnoser]
public class EnrichPhaseBenchmark : ProjectionBenchmarkBase
{
    [Params(10, 100)] public override int OrderCount { get; set; }

    private List<OrderDto> _dtos = null!;
    private string[] _customerIds = null!;
    private readonly string[] _customerExpressions = ["Name", "Email", "ProvinceId"];
    private MethodInfo _discover = null!;
    private MethodInfo _mapResponse = null!;
    private ItemsResponse<DataResponse> _customerResponse = null!;
    private PropertyDescriptor[] _descriptors = null!;
    private DistributedMapper _mapper = null!;
    private AsyncServiceScope _scope;

    protected override void Configure()
    {
        _scope = _provider.CreateAsyncScope();
        var db = _scope.ServiceProvider.GetRequiredService<ProjectionDbContext>();
        _dtos = db.Orders.AsNoTracking().Select(o => new OrderDto
        {
            Id = o.Id, CustomerId = o.CustomerId,
            Items = o.Items.Select(i => new OrderItemDto { Id = i.Id, ProductId = i.ProductId }).ToList()
        }).ToList();
        _mapper = new DistributedMapper(_scope.ServiceProvider);
        _mapper.MapDataAsync(_dtos).GetAwaiter().GetResult(); // fills ProvinceId/CategoryId too
        _customerIds = _dtos.Select(d => d.CustomerId).Distinct().ToArray();

        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        _discover = typeof(DistributedMapper).GetMethod("DiscoverResolvableProperties", flags)!;
        _mapResponse = typeof(DistributedMapper).GetMethod("MapResponseData", flags)!;
        _descriptors = Discover();
        _customerResponse = FetchCustomer().GetAwaiter().GetResult();
    }

    protected override async Task CleanupCore() => await _scope.DisposeAsync();

    private PropertyDescriptor[] Discover() =>
        ((IEnumerable<PropertyDescriptor>)_discover.Invoke(_mapper, [_dtos])!).ToArray();

    private Task<ItemsResponse<DataResponse>> FetchCustomer() =>
        _mapper.FetchDataAsync<CustomerKey>(new DistributedMapRequest(_customerIds, _customerExpressions));

    /// <summary>Everything: discover + 4 fetches (2 levels) + map back. Fresh scope per call, DTOs reused.</summary>
    [Benchmark(Baseline = true)]
    public async Task Full()
    {
        await using var scope = _provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDistributedMapper>().MapDataAsync(_dtos);
    }

    /// <summary>Walking the object graph and building PropertyDescriptors (level 0).</summary>
    [Benchmark]
    public int Discover_Level0() => Discover().Length;

    /// <summary>One FetchDataAsync: send pipelines (retry/routing/exception) + received pipelines + handler + EF.</summary>
    [Benchmark]
    public Task<ItemsResponse<DataResponse>> Fetch_Customer() => FetchCustomer();

    /// <summary>The EF handler alone: BuildFilter/BuildProjection, new scope, query, transform to DataResponse.</summary>
    [Benchmark]
    public async Task<ItemsResponse<DataResponse>> Handler_Customer()
    {
        await using var scope = _provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IQueryOfHandler<Customer, CustomerKey>>();
        var ctx = new RequestContextImpl<CustomerKey>(
            new MapRequest<CustomerKey>(_customerIds, _customerExpressions), [], CancellationToken.None);
        return await handler.GetDataAsync(ctx);
    }

    /// <summary>Raw EF equivalent: the floor for the database part.</summary>
    [Benchmark]
    public async Task<object[][]> EfDirect_Customer()
    {
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ProjectionDbContext>();
        return await db.Customers.AsNoTracking().Where(c => _customerIds.Contains(c.Id))
            .Select(c => new object[] { c.Id, c.Name, c.Email, c.ProvinceId }).ToArrayAsync();
    }

    /// <summary>Join descriptors with fetched data, JSON-deserialize each value, set through accessors.</summary>
    [Benchmark]
    public void MapResponseData_Customer()
    {
        var props = _descriptors.Where(d => d.Property.RuntimeDistributedKeyType == typeof(CustomerKey)).ToList();
        foreach (var p in props) p.EffectiveExpression = ExpressionOf(p);
        var fetched = new[] { (DistributedKeyType: typeof(CustomerKey), ItemsResponse: _customerResponse) };
        _mapResponse.Invoke(_mapper, [props, fetched]);
    }

    private static string ExpressionOf(PropertyDescriptor d) => d.PropertyInfo.Name switch
    {
        nameof(OrderDto.CustomerName) => "Name",
        nameof(OrderDto.CustomerEmail) => "Email",
        nameof(OrderDto.ProvinceId) => "ProvinceId",
        _ => d.PropertyInfo.Name
    };
}
