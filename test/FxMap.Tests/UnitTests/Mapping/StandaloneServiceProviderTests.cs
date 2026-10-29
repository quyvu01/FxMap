using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.Delegates;
using FxMap.Implementations;
using FxMap.Responses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>The lookup behind FxMapBuilder: the FxMap types it answers, and what it hands to the services of the caller.</summary>
public class StandaloneServiceProviderTests
{
    public sealed class Clock;

    public sealed class User;

    private sealed class NoClient : IRequestClient
    {
        public Task<ItemsResponse<DataResponse>> RequestAsync<TDistributedKey>(
            RequestContext<TDistributedKey> requestContext) where TDistributedKey : IDistributedKey =>
            Task.FromResult(new ItemsResponse<DataResponse>([]));
    }

    private static readonly MapperConfiguration Configuration =
        new([], [], new Dictionary<Type, Type>(), 128, 128, false, TimeSpan.FromSeconds(30), null, null);

    private static StandaloneServiceProvider Provider(IServiceProvider services = null,
        IReadOnlyDictionary<Type, object> local = null, IRequestClient client = null) =>
        new(Configuration, _ => null, client, [], local ?? new Dictionary<Type, object>(), services,
            NullLoggerFactory.Instance);

    [Fact]
    public void Answers_the_types_of_FxMap()
    {
        var client = new NoClient();
        var provider = Provider(client: client);

        provider.GetService<IServiceProvider>().ShouldBeSameAs(provider);
        provider.GetService(typeof(IMapperConfiguration)).ShouldBeSameAs(Configuration);
        provider.GetService<GetProfileConfig>().ShouldNotBeNull();
        provider.GetService<IRequestClient>().ShouldBeSameAs(client);
        provider.GetService<ILoggerFactory>().ShouldBeSameAs(NullLoggerFactory.Instance);
        provider.GetService<ILogger<User>>().ShouldNotBeNull();
    }

    [Fact]
    public void Builds_the_generic_types_of_a_key_once()
    {
        var provider = Provider(client: new NoClient());

        var orchestrator = provider.GetService(typeof(SendPipelinesOrchestrator<UserKey>));
        orchestrator.ShouldBeOfType<SendPipelinesOrchestrator<UserKey>>();
        provider.GetService(typeof(SendPipelinesOrchestrator<UserKey>)).ShouldBeSameAs(orchestrator);
        provider.GetService<IClientRequestHandler<UserKey>>().ShouldNotBeNull();

        var behaviors = provider.GetServices<ISendPipelineBehavior<UserKey>>().ToArray();
        behaviors.Select(b => b.GetType().Name).ShouldBe([
            "SendPipelineRoutingBehavior`1", "ExceptionPipelineBehavior`1", "RetryPipelineBehavior`1"
        ]);
        provider.GetServices<ISendPipelineBehavior<UserKey>>().ShouldBeSameAs(
            provider.GetService(typeof(IEnumerable<ISendPipelineBehavior<UserKey>>)));
    }

    [Fact]
    public void Answers_a_local_orchestrator_only_for_its_model_and_key()
    {
        var local = new ReceivedPipelinesOrchestrator<User, UserKey>([], [], []);
        var provider = Provider(local: new Dictionary<Type, object> { [typeof(UserKey)] = local });

        provider.GetService(typeof(ReceivedPipelinesOrchestrator<User, UserKey>)).ShouldBeSameAs(local);
        provider.GetService(typeof(ReceivedPipelinesOrchestrator<Clock, UserKey>)).ShouldBeNull();
        provider.GetService(typeof(ReceivedPipelinesOrchestrator<User, ProductKey>)).ShouldBeNull();
    }

    [Fact]
    public void Hands_the_other_types_to_the_services_of_the_caller()
    {
        var clock = new Clock();
        var services = new ServiceCollection().AddSingleton(clock).BuildServiceProvider();

        Provider(services).GetService<Clock>().ShouldBeSameAs(clock);
        Provider(services).GetService<IEnumerable<Clock>>()!.ShouldHaveSingleItem().ShouldBeSameAs(clock);
        Provider().GetService<Clock>().ShouldBeNull();
        Provider().GetService<IEnumerable<Clock>>().ShouldBeNull();
    }
}
