using System.Collections.Concurrent;
using System.Diagnostics;
using FxMap.Abstractions;
using FxMap.Extensions;
using FxMap.Registries;
using FxMap.Responses;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Tests.UnitTests.Mapping;

public sealed record RemoteCall(int Seq, Type KeyType, string[] Ids, string[] Expressions, long StartTs, long EndTs,
    bool CancellationRequested);

/// <summary>A value the fake remote sends back verbatim (to simulate malformed payloads).</summary>
public sealed record RawJson(string Json);

/// <summary>
/// In-memory stand-in for "the other service". Plugs in as the IClientRequestHandler of each distributed key, so
/// MapDataAsync runs its real pipeline (retry / routing / exception) but never touches a transport or a database.
/// </summary>
public sealed class FakeRemote
{
    /// <summary>Return this from a resolver to leave an expression out of the response.</summary>
    public static readonly object Absent = new();

    private sealed record Resolver(Func<string, string, object> Value, Func<string, bool> Exists);

    private readonly ConcurrentDictionary<Type, Resolver> _resolvers = new();
    private readonly ConcurrentQueue<RemoteCall> _calls = new();
    private int _seq;

    /// <summary>Hook executed for every request, before the response is built (delay, gate, throw...).</summary>
    public Func<Type, Task> OnRequest { get; set; }

    public IReadOnlyList<RemoteCall> Calls => [.._calls.OrderBy(c => c.Seq)];

    public IReadOnlyList<RemoteCall> CallsOf<TKey>() where TKey : IDistributedKey =>
        [..Calls.Where(c => c.KeyType == typeof(TKey))];

    /// <summary>Same as <see cref="CallsOf{TKey}"/> but ignores requests that carried no id at all.</summary>
    public IReadOnlyList<RemoteCall> NonEmptyCallsOf<TKey>() where TKey : IDistributedKey =>
        [..CallsOf<TKey>().Where(c => c.Ids.Length > 0)];

    public FakeRemote Setup<TKey>(Func<string, string, object> value, Func<string, bool> exists = null)
        where TKey : IDistributedKey
    {
        _resolvers[typeof(TKey)] = new Resolver(value, exists ?? (_ => true));
        return this;
    }

    internal async Task<ItemsResponse<DataResponse>> HandleAsync(Type keyType, string[] ids, string[] expressions,
        CancellationToken token)
    {
        var seq = Interlocked.Increment(ref _seq);
        var start = Stopwatch.GetTimestamp();
        if (OnRequest is not null) await OnRequest(keyType);
        var responses = new List<DataResponse>();
        if (_resolvers.TryGetValue(keyType, out var resolver))
            foreach (var id in ids.Where(id => resolver.Exists(id)))
                responses.Add(new DataResponse
                {
                    Id = id,
                    Values =
                    [
                        ..expressions
                            .Select(e => (Expression: e, Value: resolver.Value(id, e)))
                            .Where(x => !ReferenceEquals(x.Value, Absent))
                            .Select(x => new ValueResponse
                            {
                                Expression = x.Expression,
                                Value = x.Value is RawJson raw
                                    ? raw.Json
                                    : System.Text.Json.JsonSerializer.Serialize(x.Value)
                            })
                    ]
                });
        _calls.Enqueue(new RemoteCall(seq, keyType, ids, expressions, start, Stopwatch.GetTimestamp(),
            token.IsCancellationRequested));
        return new ItemsResponse<DataResponse>([..responses]);
    }
}

internal sealed class FakeClientHandler<TKey>(FakeRemote remote) : IClientRequestHandler<TKey>
    where TKey : IDistributedKey
{
    public Task<ItemsResponse<DataResponse>> RequestAsync(RequestContext<TKey> requestContext) =>
        remote.HandleAsync(typeof(TKey), requestContext.Query.SelectorIds, requestContext.Query.Expressions,
            requestContext.CancellationToken);
}

public sealed class MappingHarness : IDisposable
{
    private static readonly Type[] AllKeys =
        [typeof(UserKey), typeof(ProvinceKey), typeof(CountryKey), typeof(ProductKey), typeof(CategoryKey)];

    private static readonly Type[] AllProfiles =
    [
        typeof(FlatDtoProfile), typeof(GuidSelectorDtoProfile), typeof(IntSelectorDtoProfile),
        typeof(NullExpressionDtoProfile), typeof(TwoSelectorsDtoProfile), typeof(ChainDtoProfile),
        typeof(DiamondDtoProfile), typeof(ItemDtoProfile), typeof(OrderDtoProfile), typeof(NodeProfile),
        typeof(DerivedItemProfile), typeof(BlobDtoProfile), typeof(AddressDtoProfile), typeof(ProfileDtoProfile),
        typeof(ConditionalDtoProfile), typeof(GNodeProfile), typeof(LeafHolderDtoProfile)
    ];

    private readonly ServiceProvider _provider;

    public FakeRemote Remote { get; }
    public ModeFlag Mode { get; }
    public IDistributedMapper Mapper => _provider.GetRequiredService<IDistributedMapper>();

    private MappingHarness(FakeRemote remote, ModeFlag mode, ServiceProvider provider)
    {
        Remote = remote;
        Mode = mode;
        _provider = provider;
    }

    /// <summary>Builds a container with the standard test remote already wired for every key.</summary>
    public static MappingHarness Create(Action<MapConfigurator> configure = null, Action<FakeRemote> remote = null)
    {
        var fake = new FakeRemote();
        // Conventions used by most tests: Name/Email/ProvinceId/CountryId/CategoryId are derived from the id.
        fake.Setup<UserKey>(Standard("user"));
        fake.Setup<ProvinceKey>(Standard("province"));
        fake.Setup<CountryKey>(Standard("country"));
        fake.Setup<ProductKey>(Standard("product"));
        fake.Setup<CategoryKey>(Standard("category"));
        remote?.Invoke(fake);

        var mode = new ModeFlag();
        var services = new ServiceCollection();
        services.AddSingleton(fake);
        services.AddSingleton(mode);
        foreach (var key in AllKeys)
            services.AddTransient(typeof(IClientRequestHandler<>).MakeGenericType(key),
                typeof(FakeClientHandler<>).MakeGenericType(key));
        services.AddFxMap(cfg =>
        {
            cfg.AddProfileConfigs(AllProfiles);
            configure?.Invoke(cfg);
        });
        return new MappingHarness(fake, mode, services.BuildServiceProvider());
    }

    public Task Map(object value, CancellationToken token = default) => Mapper.MapDataAsync(value, token);

    /// <summary>"Name" -> "user-name:{id}", "ProvinceId" -> "province-id:{id}" ... a deterministic function of the id.</summary>
    public static Func<string, string, object> Standard(string kind) => (id, expression) => expression switch
    {
        "Name" => $"{kind}-name:{id}",
        "Email" => $"{kind}-email:{id}",
        "ProvinceId" => $"province-id:{id}",
        "CountryId" => $"country-id:{id}",
        "CategoryId" => $"category-id:{id}",
        _ => FakeRemote.Absent
    };

    public void Dispose() => _provider.Dispose();
}
