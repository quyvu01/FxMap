using System.Text.Json;
using FxMap.Abstractions;
using FxMap.Extensions;
using FxMap.Fluent;
using FxMap.HotChocolate.Extensions;
using FxMap.Tests.UnitTests.Mapping;
using HotChocolate.Execution;
using HotChocolate.Execution.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Tests.UnitTests.GraphQl;

/// <summary>A GraphQL type whose fields come from other services: a user, and the province of the user.</summary>
public class GqlMember
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string UserEmail { get; set; }
    public string ProvinceId { get; set; }
    public string ProvinceName { get; set; }
}

internal sealed class GqlMemberProfile : ProfileOf<GqlMember>
{
    protected override void Configure()
    {
        UseDistributedKey<UserKey>().Of(x => x.UserId)
            .For(x => x.UserName, "Name")
            .For(x => x.UserEmail, "Email")
            .For(x => x.ProvinceId, "ProvinceId");
        UseDistributedKey<ProvinceKey>().Of(x => x.ProvinceId)
            .For(x => x.ProvinceName, "Name");
    }
}

/// <summary>The key is computed: Mrn + Code.</summary>
public class GqlLab
{
    public string Mrn { get; set; }
    public string Code { get; set; }
    public string Label { get; set; }
}

internal sealed class GqlLabProfile : ProfileOf<GqlLab>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.Mrn + x.Code).For(x => x.Label, "Name");
}

/// <summary>What is switched on for the request being executed (a scoped service, like the user of a request).</summary>
public sealed class GqlRequestFlag
{
    public bool ShowEmail { get; set; }
}

/// <summary>The expression depends on the request.</summary>
public class GqlChoice
{
    public string UserId { get; set; }
    public string Display { get; set; }
}

internal sealed class GqlChoiceProfile : ProfileOf<GqlChoice>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.UserId)
            .For(x => x.Display, c => c
                .If(sp => sp.GetRequiredService<GqlRequestFlag>().ShowEmail)
                .Expression("Email")
                .Else("Name"));
}

/// <summary>A chain whose first link is a conditional rule: UserId -> ProvinceId (conditional) -> ProvinceName.</summary>
public class GqlChain
{
    public string UserId { get; set; }
    public string ProvinceId { get; set; }
    public string ProvinceName { get; set; }
}

internal sealed class GqlChainProfile : ProfileOf<GqlChain>
{
    protected override void Configure()
    {
        UseDistributedKey<UserKey>().Of(x => x.UserId)
            .For(x => x.ProvinceId, c => c.If(sp => sp.GetRequiredService<GqlRequestFlag>().ShowEmail)
                .Expression("ProvinceId").Else("ProvinceId"));
        UseDistributedKey<ProvinceKey>().Of(x => x.ProvinceId).For(x => x.ProvinceName, "Name");
    }
}

public class GqlQuery
{
    public List<GqlChain> GetChains() => [new() { UserId = "u1" }];

    public List<GqlMember> GetMembers() =>
    [
        new() { Id = "1", UserId = "u1" }, new() { Id = "2", UserId = "u2" }, new() { Id = "3", UserId = "u1" }
    ];

    public List<GqlMember> GetMembersWithoutUser() => [new() { Id = "9" }];

    public List<GqlLab> GetLabs() => [new() { Mrn = "M1", Code = "A" }, new() { Mrn = "M2", Code = "B" }];

    public List<GqlChoice> GetChoices() => [new() { UserId = "u1" }];
}

/// <summary>
/// A real GraphQL schema (Hot Chocolate) with the FxMap integration, executed in process. The services behind the keys are
/// the fake remote of the mapper tests, so each test sees the requests that the fields made.
/// </summary>
public sealed class GraphQlHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private IRequestExecutor _executor;

    public FakeRemote Remote { get; }

    private GraphQlHarness(FakeRemote remote, ServiceProvider provider)
    {
        Remote = remote;
        _provider = provider;
    }

    public static GraphQlHarness Create(bool throwIfException = true, Action<FakeRemote> remote = null)
    {
        var fake = new FakeRemote();
        fake.Setup<UserKey>(MappingHarness.Standard("user"));
        fake.Setup<ProvinceKey>(MappingHarness.Standard("province"));
        remote?.Invoke(fake);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(fake);
        services.AddScoped<GqlRequestFlag>();
        services.AddTransient<IClientRequestHandler<UserKey>, FakeClientHandler<UserKey>>();
        services.AddTransient<IClientRequestHandler<ProvinceKey>, FakeClientHandler<ProvinceKey>>();
        var graphQl = services.AddGraphQLServer().AddQueryType<GqlQuery>();
        services.AddFxMap(cfg =>
            {
                cfg.AddProfileConfigs(typeof(GqlMemberProfile), typeof(GqlLabProfile), typeof(GqlChoiceProfile),
                    typeof(GqlChainProfile));
                if (throwIfException) cfg.ThrowIfException();
            })
            .AddHotChocolate(cfg => cfg.AddRequestExecutorBuilder(graphQl));
        return new GraphQlHarness(fake, services.BuildServiceProvider());
    }

    /// <summary>Executes a query inside the scope of a request, and returns the JSON of the result.</summary>
    public async Task<JsonElement> QueryAsync(string query, Action<GqlRequestFlag> request = null)
    {
        _executor ??= await _provider.GetRequiredService<IRequestExecutorResolver>().GetRequestExecutorAsync();
        await using var scope = _provider.CreateAsyncScope();
        request?.Invoke(scope.ServiceProvider.GetRequiredService<GqlRequestFlag>());
        var result = await _executor.ExecuteAsync(OperationRequestBuilder.New().SetDocument(query)
            .SetServices(scope.ServiceProvider).Build());
        using var stream = new MemoryStream();
        await new JsonResultFormatter().FormatAsync(result, stream);
        return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
    }

    // The request executor resolver of Hot Chocolate can only be disposed asynchronously
    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
