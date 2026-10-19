using FxMap.Abstractions;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.Fluent;
using FxMap.Registries;
using FxMap.Supervision;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>AddFxMap(cfg => ...): the defaults, the limits that are checked, and the registration of profiles and entity configurations.</summary>
public class MapConfiguratorTests
{
    public class Thing
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class ThingView
    {
        public string ThingId { get; set; } = "";
        public string ThingName { get; set; }
    }

    public sealed class MarkerA;

    public sealed class MarkerB;

    // The configurations are open generic types, closed by a marker where a test needs them: the tests that scan the whole assembly
    // (AddEntitiesFromAssemblyContaining...) skip an open generic type, and would find two configurations of Thing otherwise.
    public class ThingConfigOf<TMarker> : EntityConfigureOf<Thing>
    {
        protected override void Configure()
        {
            Id(x => x.Id);
            DefaultProperty(x => x.Name);
            UseDistributedKey<UserKey>();
        }
    }

    public class ThingViewProfileOf<TMarker> : ProfileOf<ThingView>
    {
        protected override void Configure() =>
            UseDistributedKey<UserKey>().Of(x => x.ThingId).For(x => x.ThingName, "Name");
    }

    /// <summary>A profile that is derived from another one: it replaces it.</summary>
    public sealed class DerivedThingViewProfile<TMarker> : ThingViewProfileOf<TMarker>
    {
        protected override void Configure() =>
            UseDistributedKey<UserKey>().Of(x => x.ThingId).For(x => x.ThingName, "Email");
    }

    private static ServiceProvider Build(Action<IMapConfigurator> configure)
    {
        var services = new ServiceCollection();
        services.AddFxMap(configure);
        return services.BuildServiceProvider();
    }

    private static IMapperConfiguration Configuration(Action<IMapConfigurator> configure)
    {
        using var provider = Build(configure);
        return provider.GetRequiredService<IMapperConfiguration>();
    }

    [Fact]
    public void The_defaults()
    {
        var c = Configuration(_ => { });

        c.MaxNestingDepth.ShouldBe(128);
        c.MaxConcurrentProcessing.ShouldBe(128);
        c.ThrowIfExceptions.ShouldBeFalse();
        c.DefaultRequestTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        c.RetryPolicy.ShouldBeNull();
        c.SupervisorOptions.ShouldBeNull();
    }

    [Fact]
    public void What_is_set_is_what_the_mapper_reads()
    {
        var c = Configuration(cfg =>
        {
            cfg.SetMaxNestingDepth(7);
            cfg.SetMaxConcurrentProcessing(9);
            cfg.ThrowIfException();
            cfg.SetRequestTimeOut(TimeSpan.FromSeconds(5));
            cfg.SetRetryPolicy(2);
        });

        c.MaxNestingDepth.ShouldBe(7);
        c.MaxConcurrentProcessing.ShouldBe(9);
        c.ThrowIfExceptions.ShouldBeTrue();
        c.DefaultRequestTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        c.RetryPolicy.RetryCount.ShouldBe(2);
    }

    [Fact]
    public void The_limits_that_make_no_sense_are_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Build(c => c.SetMaxNestingDepth(-1)));
        Should.Throw<ArgumentOutOfRangeException>(() => Build(c => c.SetMaxConcurrentProcessing(0)));
        Should.Throw<ArgumentOutOfRangeException>(() => Build(c => c.SetMaxConcurrentProcessing(-5)));
        Should.Throw<ArgumentOutOfRangeException>(() => Build(c => c.SetRequestTimeOut(TimeSpan.FromSeconds(-1))));
        Should.Throw<ArgumentOutOfRangeException>(() => Build(c => c.SetRetryPolicy(-1)));
    }

    [Fact]
    public void The_edges_of_the_limits_are_accepted()
    {
        var c = Configuration(cfg =>
        {
            cfg.SetMaxNestingDepth(0);
            cfg.SetMaxConcurrentProcessing(1);
            cfg.SetRequestTimeOut(TimeSpan.Zero);
            cfg.SetRetryPolicy(0);
        });

        c.MaxNestingDepth.ShouldBe(0);
        c.MaxConcurrentProcessing.ShouldBe(1);
        c.DefaultRequestTimeout.ShouldBe(TimeSpan.Zero);
        c.RetryPolicy.RetryCount.ShouldBe(0);
    }

    [Fact]
    public void The_supervisor_options_are_kept_and_a_second_call_changes_the_same_options()
    {
        var c = Configuration(cfg =>
        {
            cfg.ConfigureSupervisor(o => o.MaxRestarts = 5);
            cfg.ConfigureSupervisor(o => o.Strategy = SupervisionStrategy.OneForAll);
        });

        c.SupervisorOptions.MaxRestarts.ShouldBe(5);
        c.SupervisorOptions.Strategy.ShouldBe(SupervisionStrategy.OneForAll);
    }

    [Fact]
    public void Scanning_an_assembly_without_profiles_or_entity_configurations_adds_none()
    {
        var configurator = new MapConfigurator(new ServiceCollection());

        configurator.AddProfilesFromAssemblies(typeof(IMapConfigurator).Assembly);
        configurator.AddEntitiesFromAssemblies(typeof(IMapConfigurator).Assembly);
        configurator.AddProfilesFromAssemblyContaining<IMapConfigurator>();
        configurator.AddEntitiesFromAssemblyContaining<IMapConfigurator>();

        configurator.ProfileConfigs.ShouldBeEmpty();
        configurator.EntityConfigs.ShouldBeEmpty();
    }

    [Fact]
    public void A_profile_that_derives_from_another_one_replaces_it()
    {
        var configurator = new MapConfigurator(new ServiceCollection());

        configurator.AddProfileConfigs(typeof(ThingViewProfileOf<MarkerA>), typeof(DerivedThingViewProfile<MarkerA>));

        configurator.ProfileConfigs[typeof(ThingView)].ShouldBeOfType<DerivedThingViewProfile<MarkerA>>();
    }

    [Fact]
    public void Two_profiles_for_the_same_model_are_ambiguous()
    {
        var configurator = new MapConfigurator(new ServiceCollection());

        var error = Should.Throw<DistributedMapException.AmbiguousProfileConfiguration>(() =>
            configurator.AddProfileConfigs(typeof(ThingViewProfileOf<MarkerA>), typeof(ThingViewProfileOf<MarkerB>)));

        error.Message.ShouldContain(nameof(MarkerA));
        error.Message.ShouldContain(nameof(MarkerB));
        error.Message.ShouldContain(typeof(ThingView).FullName!);
    }

    [Fact]
    public void Two_entity_configurations_for_the_same_entity_are_ambiguous()
    {
        var configurator = new MapConfigurator(new ServiceCollection());

        var error = Should.Throw<DistributedMapException.AmbiguousEntityConfiguration>(() =>
            configurator.AddEntityConfigs(typeof(ThingConfigOf<MarkerA>), typeof(ThingConfigOf<MarkerB>)));

        error.Message.ShouldContain(typeof(Thing).FullName!);
    }

    [Fact]
    public void A_profile_that_is_registered_twice_is_registered_once()
    {
        var configurator = new MapConfigurator(new ServiceCollection());

        configurator.AddProfileConfig<ThingViewProfileOf<MarkerA>>();
        Should.NotThrow(() => configurator.AddProfileConfig<ThingViewProfileOf<MarkerA>>());
        Should.NotThrow(() => configurator.AddProfileConfigs(typeof(ThingViewProfileOf<MarkerA>)));

        configurator.ProfileConfigs.Count.ShouldBe(1);
    }

    [Fact]
    public void An_entity_configuration_that_is_registered_twice_is_registered_once()
    {
        var configurator = new MapConfigurator(new ServiceCollection());

        configurator.AddEntityConfig<ThingConfigOf<MarkerA>>();
        Should.NotThrow(() => configurator.AddEntityConfig<ThingConfigOf<MarkerA>>());
        Should.NotThrow(() => configurator.AddEntityConfigs(typeof(ThingConfigOf<MarkerA>)));

        configurator.EntityConfigs.Count.ShouldBe(1);
    }

    [Fact]
    public void Types_that_are_not_closed_concrete_configurations_are_ignored()
    {
        var configurator = new MapConfigurator(new ServiceCollection());

        configurator.AddProfileConfigs(typeof(string), typeof(ProfileOf<>), typeof(IFluentProfileConfig), typeof(ThingViewProfileOf<MarkerA>));
        configurator.AddEntityConfigs(typeof(string), typeof(EntityConfigureOf<>), typeof(IFluentEntityConfig), typeof(ThingConfigOf<MarkerA>));

        configurator.ProfileConfigs.Keys.ShouldBe([typeof(ThingView)]);
        configurator.EntityConfigs.Keys.ShouldBe([typeof(Thing)]);
    }
}
