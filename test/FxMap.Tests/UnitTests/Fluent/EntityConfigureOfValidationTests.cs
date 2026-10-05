using System.Linq.Expressions;
using FxMap.Abstractions;
using FxMap.Exceptions;
using FxMap.Fluent;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Fluent;

public sealed class SelectorKey : IDistributedKey;

/// <summary>The Id and DefaultProperty selectors are validated once, when the entity is configured.</summary>
public class EntityConfigureOfValidationTests
{
    public class Staff
    {
        public string Code { get; set; } = "";
        public string Site { get; set; } = "";
        public string Name { get; set; } = "";
        public int Level { get; set; }
    }

    // Configure() runs inside the base constructor, so the test's configuration is handed over through a thread static.
    // The class is an open generic on purpose: assembly scans (AddEntitiesFromAssemblies) only register closed
    // concrete types, so this helper is never picked up as an entity configuration of the test assembly.
    private sealed class Config<TScanGuard> : EntityConfigureOf<Staff>
    {
        [ThreadStatic] private static Action<Config<TScanGuard>> _pending;

        public static Config<TScanGuard> Create(Action<Config<TScanGuard>> configure)
        {
            _pending = configure;
            try
            {
                return new Config<TScanGuard>();
            }
            finally
            {
                _pending = null;
            }
        }

        protected override void Configure() => _pending?.Invoke(this);

        public void SetId<T>(Expression<Func<Staff, T>> selector) => Id(selector);

        public void SetDefault<T>(Expression<Func<Staff, T>> selector) => DefaultProperty(selector);

        public void SetKey() => UseDistributedKey<SelectorKey>();
    }

    private static Config<object> Build(Action<Config<object>> configure) => Config<object>.Create(configure);

    #region Accepted

    [Fact]
    public void A_property_selector_is_accepted_for_the_id_and_the_default_property()
    {
        var config = Build(c =>
        {
            c.SetId(x => x.Code);
            c.SetDefault(x => x.Name);
            c.SetKey();
        });

        config.IdPropertySelector.Body.ToString().ShouldBe("x.Code");
        config.DefaultPropertyNameSelector.Body.ToString().ShouldBe("x.Name");
    }

    [Fact]
    public void A_computed_selector_is_accepted()
    {
        var config = Build(c =>
        {
            c.SetId(x => x.Code + ":" + x.Site);
            c.SetDefault(x => x.Name + " (" + x.Level + ")");
        });

        config.IdPropertySelector.ShouldNotBeNull();
        config.DefaultPropertyNameSelector.ShouldNotBeNull();
    }

    [Fact]
    public void The_default_property_is_optional()
    {
        Build(c => c.SetId(x => x.Code)).DefaultPropertyNameSelector.ShouldBeNull();
    }

    [Fact]
    public void The_selector_keeps_its_declared_result_type()
    {
        Build(c => c.SetId(x => x.Level)).IdPropertySelector.Body.Type.ShouldBe(typeof(int));
        Build(c => c.SetId(x => x.Code + x.Site)).IdPropertySelector.Body.Type.ShouldBe(typeof(string));
    }

    #endregion

    #region Rejected when configuring

    [Fact]
    public void An_entity_must_declare_its_id()
    {
        var error = Should.Throw<DistributedMapException.EntityIdNotConfigured>(() =>
            Build(c => c.SetDefault(x => x.Name)));

        error.Message.ShouldContain("Staff");
        error.Message.ShouldContain("Id(");
    }

    [Fact]
    public void The_id_cannot_be_declared_twice()
    {
        var error = Should.Throw<DistributedMapException.InvalidEntitySelector>(() =>
            Build(c =>
            {
                c.SetId(x => x.Code);
                c.SetId(x => x.Site);
            }));

        error.Message.ShouldContain("Id selector for Staff");
        error.Message.ShouldContain("more than once");
    }

    [Fact]
    public void The_default_property_cannot_be_declared_twice()
    {
        var error = Should.Throw<DistributedMapException.InvalidEntitySelector>(() =>
            Build(c =>
            {
                c.SetId(x => x.Code);
                c.SetDefault(x => x.Name);
                c.SetDefault(x => x.Site);
            }));

        error.Message.ShouldContain("DefaultProperty selector for Staff");
    }

    [Fact]
    public void A_selector_that_does_not_read_the_entity_is_rejected()
    {
        var captured = "constant";

        Should.Throw<DistributedMapException.InvalidEntitySelector>(() => Build(c => c.SetId(x => "always")))
            .Message.ShouldContain("does not read the entity");
        Should.Throw<DistributedMapException.InvalidEntitySelector>(() => Build(c => c.SetId(x => 5)));
        Should.Throw<DistributedMapException.InvalidEntitySelector>(() => Build(c => c.SetId(x => captured)));
        Should.Throw<DistributedMapException.InvalidEntitySelector>(() =>
            Build(c =>
            {
                c.SetId(x => x.Code);
                c.SetDefault(x => "static default");
            })).Message.ShouldContain("DefaultProperty");
    }

    [Fact]
    public void A_null_selector_is_rejected()
    {
        Should.Throw<ArgumentNullException>(() => Build(c => c.SetId<string>(null!)));
    }

    #endregion
}
