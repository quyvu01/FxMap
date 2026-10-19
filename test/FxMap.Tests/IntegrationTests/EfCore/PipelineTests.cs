using FxMap.Abstractions;
using FxMap.Delegates;
using FxMap.EntityFrameworkCore.Extensions;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.Models;
using FxMap.Registries;
using FxMap.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.IntegrationTests.EfCore;

public sealed class PipelineLog
{
    public List<string> Events { get; } = [];
    public void Add(string e) { lock (Events) Events.Add(e); }
}

public sealed class FirstReceived<TKey>(PipelineLog log) : IReceivedPipelineBehavior<TKey> where TKey : IDistributedKey
{
    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TKey> requestContext, ReceivedHandlerDelegate next)
    {
        log.Add($"First before [{string.Join(",", requestContext.Query.Expressions)}]");
        var result = await next();
        log.Add("First after");
        return result;
    }
}

public sealed class SecondReceived<TKey>(PipelineLog log) : IReceivedPipelineBehavior<TKey> where TKey : IDistributedKey
{
    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TKey> requestContext, ReceivedHandlerDelegate next)
    {
        log.Add("Second before");
        var result = await next();
        log.Add("Second after");
        return result;
    }
}

public sealed class DropsTheFirstItem<TKey> : IReceivedPipelineBehavior<TKey> where TKey : IDistributedKey
{
    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TKey> requestContext, ReceivedHandlerDelegate next)
    {
        var result = await next();
        return new ItemsResponse<DataResponse>([..result.Items.Skip(1)]);
    }
}

public sealed class AnswersWithoutTheHandler<TKey> : IReceivedPipelineBehavior<TKey> where TKey : IDistributedKey
{
    public Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TKey> requestContext, ReceivedHandlerDelegate next) =>
        Task.FromResult(new ItemsResponse<DataResponse>([
            new DataResponse { Id = "fixed", Values = [new ValueResponse { Expression = "Name", Value = "\"from the pipeline\"" }] }
        ]));
}

public sealed class FailsTheRequest<TKey> : IReceivedPipelineBehavior<TKey> where TKey : IDistributedKey
{
    public Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TKey> requestContext, ReceivedHandlerDelegate next) =>
        throw new InvalidOperationException("the pipeline failed");
}

public sealed class GreetingExpression : ICustomExpressionBehavior<StaffKey>
{
    public string CustomExpression() => "Greeting";

    public Task<Dictionary<string, object>> HandleAsync(RequestContext<StaffKey> requestContext) =>
        Task.FromResult(requestContext.Query.SelectorIds.ToDictionary(id => id, object (id) => $"Hello {id}"));
}

public sealed class CountExpression : ICustomExpressionBehavior<StaffKey>
{
    public string CustomExpression() => "IdLength";

    public Task<Dictionary<string, object>> HandleAsync(RequestContext<StaffKey> requestContext) =>
        Task.FromResult(requestContext.Query.SelectorIds.ToDictionary(id => id, object (id) => id.Length));
}

/// <summary>The pipelines that the owner runs around its handler, and the custom expressions it answers itself.</summary>
[Collection("EfCore Sequential")]
public class ReceivedPipelineTests
{
    private static ServiceProvider Create(Action<IMapConfigurator> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton<PipelineLog>();
        var dbName = $"staff_{Guid.NewGuid()}"; // once: a name built inside the lambda would be a new database for every DbContext
        services.AddDbContext<StaffDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddFxMap(cfg =>
            {
                cfg.AddEntityConfigs(typeof(StaffConfig));
                cfg.AddProfileConfigs(typeof(StaffViewProfile));
                cfg.ThrowIfException();
                configure(cfg);
            })
            .AddEntityFrameworkCore(c => c.AddDbContexts(typeof(StaffDbContext)));
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StaffDbContext>();
        db.Staff.AddRange(
            new StaffRow { Id = 1, Code = "A", Facility = "F1", Name = "Anna", Level = 3 },
            new StaffRow { Id = 2, Code = "B", Facility = "F1", Name = "Bao", Level = 2 });
        db.SaveChanges();
        return provider;
    }

    private static async Task<ItemsResponse<DataResponse>> Fetch(ServiceProvider provider, string[] ids, params string[] expressions)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDistributedMapper>()
            .FetchDataAsync<StaffKey>(new DistributedMapRequest(ids, expressions));
    }

    private static string ValueOf(DataResponse row, string expression) =>
        row.Values.Single(v => v.Expression == expression).Value;

    [Fact]
    public async Task A_pipeline_runs_around_the_handler_and_sees_the_request()
    {
        using var provider = Create(c => c.AddReceivedPipelines(p => p.OfType(typeof(FirstReceived<>))));

        var response = await Fetch(provider, ["A:F1"], "Name", "Level");

        response.Items.ShouldHaveSingleItem();
        ValueOf(response.Items[0], "Level").ShouldBe("3");
        provider.GetRequiredService<PipelineLog>().Events.ShouldBe(["First before [Name,Level]", "First after"]);
    }

    [Fact]
    public async Task The_first_pipeline_registered_is_the_outermost()
    {
        using var provider = Create(c => c.AddReceivedPipelines(p => p
            .OfType(typeof(FirstReceived<>)).OfType(typeof(SecondReceived<>))));

        await Fetch(provider, ["A:F1"], "Name");

        provider.GetRequiredService<PipelineLog>().Events
            .ShouldBe(["First before [Name]", "Second before", "Second after", "First after"]);
    }

    [Fact]
    public async Task A_closed_pipeline_runs_for_its_key()
    {
        using var provider = Create(c => c.AddReceivedPipelines(p => p.OfType<FirstReceived<StaffKey>>()));

        await Fetch(provider, ["A:F1"], "Name");

        provider.GetRequiredService<PipelineLog>().Events.ShouldContain("First after");
    }

    [Fact]
    public async Task A_pipeline_can_change_the_response()
    {
        using var provider = Create(c => c.AddReceivedPipelines(p => p.OfType(typeof(DropsTheFirstItem<>))));

        var response = await Fetch(provider, ["A:F1", "B:F1"], "Name");

        response.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_pipeline_can_answer_without_the_handler()
    {
        using var provider = Create(c => c.AddReceivedPipelines(p => p.OfType(typeof(AnswersWithoutTheHandler<>))));

        var response = await Fetch(provider, ["A:F1"], "Name");

        var row = response.Items.ShouldHaveSingleItem();
        row.Id.ShouldBe("fixed");
        ValueOf(row, "Name").ShouldBe("\"from the pipeline\"");
    }

    [Fact]
    public async Task The_exception_of_a_pipeline_fails_the_request()
    {
        using var provider = Create(c => c.AddReceivedPipelines(p => p.OfType(typeof(FailsTheRequest<>))));

        var error = await Should.ThrowAsync<InvalidOperationException>(() => Fetch(provider, ["A:F1"], "Name"));

        error.Message.ShouldBe("the pipeline failed");
    }

    [Fact]
    public async Task A_custom_expression_is_answered_by_its_behavior_next_to_the_expressions_of_the_handler()
    {
        using var provider = Create(c => c.AddCustomExpressionPipelines(p => p.OfType<GreetingExpression>()));

        var response = await Fetch(provider, ["A:F1", "B:F1"], "Name", "Greeting");

        response.Items.Count().ShouldBe(2);
        foreach (var row in response.Items)
        {
            ValueOf(row, "Greeting").ShouldBe($"\"Hello {row.Id}\"");
            ValueOf(row, "Name").ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task A_request_of_a_custom_expression_only_is_answered()
    {
        using var provider = Create(c => c.AddCustomExpressionPipelines(p => p.OfType<GreetingExpression>()));

        var response = await Fetch(provider, ["A:F1"], "Greeting");

        var row = response.Items.ShouldHaveSingleItem();
        ValueOf(row, "Greeting").ShouldBe("\"Hello A:F1\"");
    }

    [Fact]
    public async Task Several_custom_expressions_are_answered_together_with_a_number_as_well_as_a_text()
    {
        using var provider = Create(c => c.AddCustomExpressionPipelines(p => p
            .OfType<GreetingExpression>().OfType<CountExpression>()));

        var response = await Fetch(provider, ["A:F1"], "Greeting", "IdLength", "Level");

        var row = response.Items.ShouldHaveSingleItem();
        ValueOf(row, "Greeting").ShouldBe("\"Hello A:F1\"");
        ValueOf(row, "IdLength").ShouldBe("4");
        ValueOf(row, "Level").ShouldBe("3");
    }

    [Fact]
    public async Task The_pipelines_do_not_see_the_custom_expressions_that_the_behaviors_answer()
    {
        using var provider = Create(c =>
        {
            c.AddReceivedPipelines(p => p.OfType(typeof(FirstReceived<>)));
            c.AddCustomExpressionPipelines(p => p.OfType<GreetingExpression>());
        });

        await Fetch(provider, ["A:F1"], "Name", "Greeting");

        provider.GetRequiredService<PipelineLog>().Events[0].ShouldBe("First before [Name]");
    }

    [Fact]
    public void A_type_that_is_not_a_pipeline_is_rejected_when_it_is_registered()
    {
        Should.Throw<DistributedMapException.TypeIsNotReceivedPipelineBehavior>(() =>
            Create(c => c.AddReceivedPipelines(p => p.OfType(typeof(string)))));
        Should.Throw<DistributedMapException.TypeIsNotSendPipelineBehavior>(() =>
            Create(c => c.AddSendPipelines(p => p.OfType(typeof(string)))));
        Should.Throw<DistributedMapException.TypeIsNotCustomExpressionPipelineBehavior>(() =>
            Create(c => c.AddCustomExpressionPipelines(p => p.OfType<FirstReceived<StaffKey>>())));
    }
}
