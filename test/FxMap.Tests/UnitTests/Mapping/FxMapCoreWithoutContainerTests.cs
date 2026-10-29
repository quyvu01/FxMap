using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.BuiltInPipelines;
using FxMap.Delegates;
using FxMap.Exceptions;
using FxMap.Handlers;
using FxMap.Helpers;
using FxMap.Implementations;
using FxMap.Models;
using FxMap.Responses;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>FxMap.Core works without a container: its assembly does not reference one, and its pieces take what they need.</summary>
public class FxMapCoreWithoutContainerTests
{
    [Fact]
    public void FxMap_Core_does_not_reference_dependency_injection()
    {
        var references = typeof(FxMapBuilder).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

        typeof(FxMapBuilder).Assembly.GetName().Name.ShouldBe("FxMap.Core");
        references.ShouldNotContain(n => n!.StartsWith("Microsoft.Extensions.DependencyInjection"));
        references.ShouldNotContain(n => n!.StartsWith("Microsoft.Extensions.Hosting"));
    }

    [Fact]
    public void The_container_integration_is_in_the_FxMap_package()
    {
        typeof(Extensions.DependencyExtensions).Assembly.GetName().Name.ShouldBe("FxMap");
        typeof(Registries.IMapConfigurator).Assembly.GetName().Name.ShouldBe("FxMap");
    }

    private static MapperConfiguration Config(bool throwIfExceptions = false, RetryPolicy retry = null) =>
        new([], [], new Dictionary<Type, Type>(), 128, 128, throwIfExceptions, TimeSpan.FromSeconds(30), retry, null);

    private sealed class FlakyClient(int failures) : IRequestClient
    {
        public int Calls { get; private set; }

        public Task<ItemsResponse<DataResponse>> RequestAsync<TDistributedKey>(
            RequestContext<TDistributedKey> requestContext) where TDistributedKey : IDistributedKey
        {
            Calls++;
            if (Calls <= failures) throw new TimeoutException("the broker is away");
            return Task.FromResult(new ItemsResponse<DataResponse>([new DataResponse { Id = "u1", Values = [] }]));
        }
    }

    private sealed class Recording<TDistributedKey>(string name, List<string> log)
        : ISendPipelineBehavior<TDistributedKey> where TDistributedKey : IDistributedKey
    {
        public async Task<ItemsResponse<DataResponse>> HandleAsync(RequestContext<TDistributedKey> requestContext,
            SendHandlerDelegate next)
        {
            log.Add($"{name}>");
            var result = await next();
            log.Add($"<{name}");
            return result;
        }
    }

    private static SendPipelinesOrchestrator<UserKey> Orchestrator(IRequestClient client, MapperConfiguration config,
        params ISendPipelineBehavior<UserKey>[] behaviors) =>
        new(new ClientRequestHandler<UserKey>(client), config, behaviors);

    private static readonly DistributedMapRequest Request = new(["u1"], ["Name"]);

    [Fact]
    public async Task The_behaviors_run_in_their_order_around_the_client()
    {
        var log = new List<string>();
        var orchestrator = Orchestrator(new FlakyClient(0), Config(),
            new Recording<UserKey>("a", log), new Recording<UserKey>("b", log));

        await orchestrator.ExecuteAsync(Request, null);

        log.ShouldBe(["a>", "b>", "<b", "<a"]);
    }

    [Fact]
    public async Task Retry_retries_with_its_configuration_only()
    {
        var config = Config(retry: new RetryPolicy(2, null, null));
        var client = new FlakyClient(failures: 2);

        var result = await Orchestrator(client, config, new RetryPipelineBehavior<UserKey>(config))
            .ExecuteAsync(Request, null);

        client.Calls.ShouldBe(3);
        result.Items.ShouldHaveSingleItem().Id.ShouldBe("u1");
    }

    [Fact]
    public async Task Exceptions_are_suppressed_unless_ThrowIfException()
    {
        var quiet = Config();
        var loud = Config(throwIfExceptions: true);

        var result = await Orchestrator(new FlakyClient(9), quiet, new ExceptionPipelineBehavior<UserKey>(quiet))
            .ExecuteAsync(Request, null);
        result.Items.ShouldBeEmpty();

        await Should.ThrowAsync<TimeoutException>(() =>
            Orchestrator(new FlakyClient(9), loud, new ExceptionPipelineBehavior<UserKey>(loud))
                .ExecuteAsync(Request, null));
    }

    [Fact]
    public async Task Without_a_client_the_key_has_no_handler() =>
        await Should.ThrowAsync<DistributedMapException.NoHandlerForDistributedKey>(() =>
            Orchestrator(null, Config()).ExecuteAsync(Request, null));

    public sealed class Clock;

    public sealed class NeedsClock(Clock clock)
    {
        public Clock Clock { get; } = clock;
    }

    public sealed class Overloads
    {
        public Overloads() => Used = "none";
        public Overloads(Clock clock) => Used = clock is null ? "null" : "clock";
        public Overloads(Clock clock, string name = "default") => Used = $"clock+{name}";
        public string Used { get; }
    }

    [Fact]
    public void ServiceActivator_takes_the_parameters_from_the_services()
    {
        var clock = new Clock();
        var services = new ServiceCollection().AddSingleton(clock).BuildServiceProvider();

        ((NeedsClock)ServiceActivator.Create(typeof(NeedsClock), services)).Clock.ShouldBeSameAs(clock);
    }

    [Fact]
    public void ServiceActivator_uses_the_largest_constructor_it_can_fill_and_the_default_values()
    {
        var withClock = new ServiceCollection().AddSingleton(new Clock()).BuildServiceProvider();

        ((Overloads)ServiceActivator.Create(typeof(Overloads), withClock)).Used.ShouldBe("clock+default");
        ((Overloads)ServiceActivator.Create(typeof(Overloads), EmptyServiceProvider.Instance)).Used.ShouldBe("none");
    }

    [Fact]
    public void ServiceActivator_says_what_it_cannot_create() =>
        Should.Throw<InvalidOperationException>(() =>
            ServiceActivator.Create(typeof(NeedsClock), EmptyServiceProvider.Instance)).Message.ShouldContain("NeedsClock");
}
