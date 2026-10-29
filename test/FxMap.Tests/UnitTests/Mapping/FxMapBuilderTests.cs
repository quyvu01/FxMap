using System.Collections.Concurrent;
using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.Fluent;
using FxMap.Responses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>FxMapBuilder: a mapper built without a service collection behaves as the one of AddFxMap.</summary>
public class FxMapBuilderTests
{
    public sealed class BuilderOrderView
    {
        public string UserId { get; set; } = "";
        public string UserName { get; set; }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
    }

    // Internal so the tests that scan the assembly do not find two profiles of BuilderOrderView.
    internal sealed class BuilderOrderViewProfile : ProfileOf<BuilderOrderView>
    {
        protected override void Configure() =>
            UseDistributedKey<UserKey>().Of(x => x.UserId).For(x => x.UserName, "Name");
    }

    internal sealed class TwoKeysProfile : ProfileOf<BuilderOrderView>
    {
        protected override void Configure()
        {
            UseDistributedKey<UserKey>().Of(x => x.UserId).For(x => x.UserName, "Name");
            UseDistributedKey<ProductKey>().Of(x => x.ProductId).For(x => x.ProductName, "Name");
        }
    }

    public sealed class Flags(bool showEmail)
    {
        public bool ShowEmail { get; } = showEmail;
    }

    internal sealed class ConditionalProfile : ProfileOf<BuilderOrderView>
    {
        protected override void Configure() =>
            UseDistributedKey<UserKey>().Of(x => x.UserId)
                .For(x => x.UserName, c => c
                    .If(sp => sp.GetService<Flags>() is { ShowEmail: true })
                    .Expression("Email")
                    .Else("Name"));
    }

    public sealed class User
    {
        public string Id { get; set; } = "";
    }

    /// <summary>Answers "{expression}-{id}" for any id; fails its first calls when asked to.</summary>
    private sealed class FakeClient(int failures = 0) : IRequestClient
    {
        private int _calls;
        public int Calls => _calls;
        public ConcurrentQueue<(Type Key, string[] Ids, string[] Expressions)> Requests { get; } = new();

        public Task<ItemsResponse<DataResponse>> RequestAsync<TDistributedKey>(
            RequestContext<TDistributedKey> requestContext) where TDistributedKey : IDistributedKey
        {
            if (Interlocked.Increment(ref _calls) <= failures) throw new TimeoutException("the broker is away");
            var query = requestContext.Query;
            Requests.Enqueue((typeof(TDistributedKey), query.SelectorIds, query.Expressions));
            return Task.FromResult(Answer("remote", query.SelectorIds, query.Expressions));
        }
    }

    private static ItemsResponse<DataResponse> Answer(string prefix, string[] ids, string[] expressions) =>
        new([
            ..ids.Select(id => new DataResponse
            {
                Id = id,
                Values = [..expressions.Select(e => new ValueResponse { Expression = e, Value = $"\"{prefix}-{e}-{id}\"" })]
            })
        ]);

    private sealed class LocalUserHandler(Exception fail = null) : IQueryOfHandler<User, UserKey>
    {
        public int Calls { get; private set; }

        public Task<ItemsResponse<DataResponse>> GetDataAsync(RequestContext<UserKey> context)
        {
            Calls++;
            if (fail is not null) throw fail;
            return Task.FromResult(Answer("local", context.Query.SelectorIds, context.Query.Expressions));
        }
    }

    private sealed class OtherUserHandler : IQueryOfHandler<BuilderOrderView, UserKey>
    {
        public Task<ItemsResponse<DataResponse>> GetDataAsync(RequestContext<UserKey> context) =>
            Task.FromResult(new ItemsResponse<DataResponse>([]));
    }

    private sealed class ListLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(string Category, LogLevel Level)> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new ListLogger(categoryName, Entries);
        public void Dispose() { }

        private sealed class ListLogger(string category, ConcurrentQueue<(string, LogLevel)> entries) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter) => entries.Enqueue((category, logLevel));
        }
    }

    private static FxMapBuilder Builder(params Type[] profiles) => FxMapBuilder.Create().AddProfiles(profiles);

    [Fact]
    public async Task Maps_without_a_service_collection_with_one_request_per_key()
    {
        var client = new FakeClient();
        var mapper = Builder(typeof(BuilderOrderViewProfile)).UseRequestClient(client).Build();
        var orders = new[]
        {
            new BuilderOrderView { UserId = "u1" }, new BuilderOrderView { UserId = "u2" },
            new BuilderOrderView { UserId = "u1" }
        };

        await mapper.MapDataAsync(orders);

        orders.Select(o => o.UserName).ShouldBe(["remote-Name-u1", "remote-Name-u2", "remote-Name-u1"]);
        var request = client.Requests.ShouldHaveSingleItem();
        request.Key.ShouldBe(typeof(UserKey));
        request.Ids.OrderBy(x => x).ShouldBe(["u1", "u2"]);
        request.Expressions.ShouldBe(["Name"]);
    }

    [Fact]
    public async Task Each_key_is_sent_on_its_own()
    {
        var client = new FakeClient();
        var mapper = Builder(typeof(TwoKeysProfile)).UseRequestClient(client).Build();
        var order = new BuilderOrderView { UserId = "u1", ProductId = "p1" };

        await mapper.MapDataAsync(order);

        order.UserName.ShouldBe("remote-Name-u1");
        order.ProductName.ShouldBe("remote-Name-p1");
        client.Requests.Select(r => r.Key).OrderBy(k => k.Name).ShouldBe([typeof(ProductKey), typeof(UserKey)]);
    }

    [Fact]
    public async Task The_retry_policy_applies()
    {
        var client = new FakeClient(failures: 2);
        var mapper = Builder(typeof(BuilderOrderViewProfile)).UseRequestClient(client)
            .SetRetryPolicy(2).ThrowIfException().Build();
        var order = new BuilderOrderView { UserId = "u1" };

        await mapper.MapDataAsync(order);

        client.Calls.ShouldBe(3);
        order.UserName.ShouldBe("remote-Name-u1");
    }

    [Fact]
    public async Task Errors_are_logged_and_suppressed_by_default()
    {
        var logs = new ListLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(b => b.AddProvider(logs));
        var mapper = Builder(typeof(BuilderOrderViewProfile)).UseRequestClient(new FakeClient(failures: 9))
            .UseLoggerFactory(loggerFactory).Build();
        var order = new BuilderOrderView { UserId = "u1" };

        await mapper.MapDataAsync(order);

        order.UserName.ShouldBeNull();
        logs.Entries.ShouldContain(e => e.Level == LogLevel.Error && e.Category.Contains("ExceptionPipelineBehavior"));
    }

    [Fact]
    public async Task Errors_are_thrown_with_ThrowIfException()
    {
        var mapper = Builder(typeof(BuilderOrderViewProfile)).UseRequestClient(new FakeClient(failures: 9))
            .ThrowIfException().Build();

        await Should.ThrowAsync<TimeoutException>(() => mapper.MapDataAsync(new BuilderOrderView { UserId = "u1" }));
    }

    [Fact]
    public async Task A_request_that_outlives_the_timeout_is_cancelled()
    {
        var mapper = Builder(typeof(BuilderOrderViewProfile)).UseRequestClient(new HangingClient())
            .SetRequestTimeOut(TimeSpan.FromMilliseconds(100)).ThrowIfException().Build();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            mapper.MapDataAsync(new BuilderOrderView { UserId = "u1" }));
    }

    private sealed class HangingClient : IRequestClient
    {
        public async Task<ItemsResponse<DataResponse>> RequestAsync<TDistributedKey>(
            RequestContext<TDistributedKey> requestContext) where TDistributedKey : IDistributedKey
        {
            await Task.Delay(Timeout.Infinite, requestContext.CancellationToken);
            return null;
        }
    }

    [Fact]
    public async Task Conditional_expressions_read_the_given_services()
    {
        var services = new ServiceCollection().AddSingleton(new Flags(showEmail: true)).BuildServiceProvider();
        var mapper = Builder(typeof(ConditionalProfile)).UseRequestClient(new FakeClient()).UseServices(services)
            .Build();
        var order = new BuilderOrderView { UserId = "u1" };

        await mapper.MapDataAsync(order);

        order.UserName.ShouldBe("remote-Email-u1");
    }

    [Fact]
    public async Task Without_services_conditional_expressions_find_none()
    {
        var mapper = Builder(typeof(ConditionalProfile)).UseRequestClient(new FakeClient()).Build();
        var order = new BuilderOrderView { UserId = "u1" };

        await mapper.MapDataAsync(order);

        order.UserName.ShouldBe("remote-Name-u1");
    }

    [Fact]
    public async Task Send_behaviors_run_in_their_order_with_their_dependencies_from_the_services()
    {
        var log = new SendLog();
        var services = new ServiceCollection().AddSingleton(log).BuildServiceProvider();
        var mapper = Builder(typeof(BuilderOrderViewProfile)).UseRequestClient(new FakeClient())
            .UseServices(services)
            .AddSendBehavior(typeof(OuterSend<>))
            .AddSendBehavior(typeof(InnerSend<>))
            .Build();

        await mapper.MapDataAsync(new BuilderOrderView { UserId = "u1" });

        log.Events.ShouldBe(["Outer before [Name] ids [u1]", "Inner before", "Inner after", "Outer after"]);
    }

    [Fact]
    public async Task A_closed_send_behavior_runs_for_its_key_only()
    {
        var client = new FakeClient();
        var mapper = Builder(typeof(TwoKeysProfile)).UseRequestClient(client)
            .AddSendBehavior(typeof(AnswersLocally<ProductKey>))
            .Build();
        var order = new BuilderOrderView { UserId = "u1", ProductId = "p1" };

        await mapper.MapDataAsync(order);

        order.ProductName.ShouldBe("cached");
        order.UserName.ShouldBe("remote-Name-u1");
        client.Requests.ShouldHaveSingleItem().Key.ShouldBe(typeof(UserKey));
    }

    [Fact]
    public void A_send_behavior_must_be_one() =>
        Should.Throw<DistributedMapException.TypeIsNotSendPipelineBehavior>(() =>
            FxMapBuilder.Create().AddSendBehavior(typeof(string)));

    [Fact]
    public void Build_needs_something_that_answers_each_key()
    {
        var error = Should.Throw<InvalidOperationException>(() => Builder(typeof(BuilderOrderViewProfile)).Build());

        error.Message.ShouldContain(nameof(UserKey));
    }

    [Fact]
    public async Task A_builder_without_profiles_needs_no_request_client()
    {
        var mapper = FxMapBuilder.Create().Build();
        var order = new BuilderOrderView { UserId = "u1" };

        await mapper.MapDataAsync(order);

        order.UserName.ShouldBeNull();
    }

    [Fact]
    public async Task A_local_handler_answers_without_a_request_client()
    {
        var handler = new LocalUserHandler();
        var mapper = Builder(typeof(BuilderOrderViewProfile)).AddQueryHandler(handler).Build();
        var order = new BuilderOrderView { UserId = "u1" };

        await mapper.MapDataAsync(order);

        order.UserName.ShouldBe("local-Name-u1");
        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task A_local_handler_wins_over_the_request_client_and_the_other_keys_are_sent()
    {
        var client = new FakeClient();
        var mapper = Builder(typeof(TwoKeysProfile)).UseRequestClient(client)
            .AddQueryHandler(new LocalUserHandler()).Build();
        var order = new BuilderOrderView { UserId = "u1", ProductId = "p1" };

        await mapper.MapDataAsync(order);

        order.UserName.ShouldBe("local-Name-u1");
        order.ProductName.ShouldBe("remote-Name-p1");
        client.Requests.ShouldHaveSingleItem().Key.ShouldBe(typeof(ProductKey));
    }

    [Fact]
    public async Task The_errors_of_a_local_handler_are_neither_retried_nor_suppressed()
    {
        var handler = new LocalUserHandler(new InvalidOperationException("the database is down"));
        var mapper = Builder(typeof(BuilderOrderViewProfile)).AddQueryHandler(handler).SetRetryPolicy(3).Build();

        var error = await Should.ThrowAsync<InvalidOperationException>(() =>
            mapper.MapDataAsync(new BuilderOrderView { UserId = "u1" }));

        error.Message.ShouldBe("the database is down");
        handler.Calls.ShouldBe(1);
    }

    [Fact]
    public void A_key_has_one_local_handler() =>
        Should.Throw<DistributedMapException.DistributedKeyAssignedToMultipleEntities>(() =>
            FxMapBuilder.Create().AddQueryHandler(new LocalUserHandler()).AddQueryHandler(new OtherUserHandler()));

    [Fact]
    public async Task Gives_the_same_result_as_AddFxMap()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRequestClient>(new FakeClient());
        services.AddFxMap(cfg => cfg.AddProfileConfigs(typeof(TwoKeysProfile)));
        await using var provider = services.BuildServiceProvider();
        var fromContainer = new BuilderOrderView { UserId = "u1", ProductId = "p1" };
        var fromBuilder = new BuilderOrderView { UserId = "u1", ProductId = "p1" };

        await provider.GetRequiredService<IDistributedMapper>().MapDataAsync(fromContainer);
        await Builder(typeof(TwoKeysProfile)).UseRequestClient(new FakeClient()).Build().MapDataAsync(fromBuilder);

        fromBuilder.UserName.ShouldBe(fromContainer.UserName);
        fromBuilder.ProductName.ShouldBe(fromContainer.ProductName);
    }

    [Fact]
    public async Task FetchDataAsync_goes_through_the_same_pipeline()
    {
        var client = new FakeClient();
        var mapper = Builder(typeof(BuilderOrderViewProfile)).UseRequestClient(client).Build();

        var result = await mapper.FetchDataAsync<UserKey>(new Models.DistributedMapRequest(["u1", "u1", null], ["Name"]));

        result.Items.ShouldHaveSingleItem().Id.ShouldBe("u1");
        client.Requests.ShouldHaveSingleItem().Ids.ShouldBe(["u1"]);
    }

    [Fact]
    public void The_arguments_are_checked()
    {
        var builder = FxMapBuilder.Create();
        Should.Throw<ArgumentNullException>(() => builder.UseRequestClient(null));
        Should.Throw<ArgumentNullException>(() => builder.UseServices(null));
        Should.Throw<ArgumentNullException>(() => builder.UseLoggerFactory(null));
        Should.Throw<ArgumentNullException>(() => builder.AddSendBehavior(null));
        Should.Throw<ArgumentNullException>(() => builder.AddQueryHandler<User, UserKey>(null));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetMaxNestingDepth(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => builder.SetRequestTimeOut(TimeSpan.FromSeconds(-1)));
    }
}
