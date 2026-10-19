using FxMap.Abstractions;
using FxMap.Delegates;
using FxMap.Extensions;
using FxMap.Responses;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

public sealed class SendLog
{
    public List<string> Events { get; } = [];
    public void Add(string e) { lock (Events) Events.Add(e); }
}

public sealed class OuterSend<TKey>(SendLog log) : ISendPipelineBehavior<TKey> where TKey : IDistributedKey
{
    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TKey> requestContext, SendHandlerDelegate next)
    {
        log.Add($"Outer before [{string.Join(",", requestContext.Query.Expressions.Where(e => e != null))}] ids [{string.Join(",", requestContext.Query.SelectorIds)}]");
        var result = await next();
        log.Add("Outer after");
        return result;
    }
}

public sealed class InnerSend<TKey>(SendLog log) : ISendPipelineBehavior<TKey> where TKey : IDistributedKey
{
    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TKey> requestContext, SendHandlerDelegate next)
    {
        log.Add("Inner before");
        var result = await next();
        log.Add("Inner after");
        return result;
    }
}

public sealed class AnswersLocally<TKey> : ISendPipelineBehavior<TKey> where TKey : IDistributedKey
{
    public Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TKey> requestContext, SendHandlerDelegate next) =>
        Task.FromResult(new ItemsResponse<DataResponse>([
            ..requestContext.Query.SelectorIds.Select(id => new DataResponse
            {
                Id = id,
                Values = [..requestContext.Query.Expressions.Select(e => new ValueResponse { Expression = e, Value = "\"cached\"" })]
            })
        ]));
}

public sealed class UppercasesTheValues<TKey> : ISendPipelineBehavior<TKey> where TKey : IDistributedKey
{
    public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TKey> requestContext, SendHandlerDelegate next)
    {
        var result = await next();
        foreach (var item in result.Items)
            foreach (var value in item.Values)
                value.Value = value.Value?.ToUpperInvariant();
        return result;
    }
}

/// <summary>The pipelines of the client: they wrap the request that leaves the service, whatever the transport.</summary>
public class SendPipelineTests
{
    private static (MappingHarness Harness, SendLog Log) Harness(Action<FxMap.Registries.IMapConfigurator> configure, Action<FakeRemote> remote = null)
    {
        var log = new SendLog();
        var h = MappingHarness.Create(c =>
        {
            c.Services.AddSingleton(log);
            configure(c);
        }, remote);
        return (h, log);
    }

    [Fact]
    public async Task A_pipeline_runs_around_each_request_and_sees_the_ids_and_the_expressions()
    {
        var (h, log) = Harness(c => c.AddSendPipelines(p => p.OfType(typeof(OuterSend<>))));
        using var _ = h;
        var dto = new FlatDto { UserId = "u1" };

        await h.Map(dto);

        dto.UserName.ShouldBe("user-name:u1");
        log.Events.Count.ShouldBe(2);
        log.Events[0].ShouldStartWith("Outer before [Name,"); // the profile of FlatDto asks several properties of the user in one request
        log.Events[0].ShouldEndWith("ids [u1]");
        log.Events[1].ShouldBe("Outer after");
    }

    [Fact]
    public async Task The_first_pipeline_registered_is_the_outermost()
    {
        var (h, log) = Harness(c => c.AddSendPipelines(p => p.OfType(typeof(OuterSend<>)).OfType(typeof(InnerSend<>))));
        using var _ = h;

        await h.Map(new FlatDto { UserId = "u1" });

        log.Events.Count.ShouldBe(4);
        log.Events[0].ShouldStartWith("Outer before");
        log.Events.Skip(1).ShouldBe(["Inner before", "Inner after", "Outer after"]);
    }

    [Fact]
    public async Task A_pipeline_can_answer_without_the_remote()
    {
        var (h, _) = Harness(c => c.AddSendPipelines(p => p.OfType(typeof(AnswersLocally<>))));
        using var _ = h;
        var dto = new FlatDto { UserId = "u1" };

        await h.Map(dto);

        dto.UserName.ShouldBe("cached");
        h.Remote.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_pipeline_can_change_the_response_before_it_is_mapped()
    {
        var (h, _) = Harness(c => c.AddSendPipelines(p => p.OfType(typeof(UppercasesTheValues<>))));
        using var _ = h;
        var dto = new FlatDto { UserId = "u1" };

        await h.Map(dto);

        dto.UserName.ShouldBe("USER-NAME:U1");
    }

    [Fact]
    public async Task One_pipeline_call_per_request_when_the_retry_policy_retries_inside_it()
    {
        // The pipelines of the user wrap the retries (the retry policy is one of the built-in pipelines, which come after)
        var attempts = 0;
        var (h, log) = Harness(c =>
        {
            c.ThrowIfException();
            c.SetRetryPolicy(2);
            c.AddSendPipelines(p => p.OfType(typeof(OuterSend<>)));
        }, remote => remote.OnRequest = _ =>
        {
            if (Interlocked.Increment(ref attempts) <= 2) throw new InvalidOperationException("boom");
            return Task.CompletedTask;
        });
        using var _ = h;
        var dto = new FlatDto { UserId = "u1" };

        await h.Map(dto);

        dto.UserName.ShouldBe("user-name:u1");
        attempts.ShouldBe(3);
        log.Events.Count(e => e.StartsWith("Outer before")).ShouldBe(1);
    }

    [Fact]
    public async Task A_closed_pipeline_runs_for_its_key_only()
    {
        var (h, log) = Harness(c => c.AddSendPipelines(p => p.OfType<OuterSend<ProvinceKey>>()));
        using var _ = h;

        await h.Map(new FlatDto { UserId = "u1" }); // asks the user key, not the province key

        log.Events.ShouldBeEmpty();
    }
}
