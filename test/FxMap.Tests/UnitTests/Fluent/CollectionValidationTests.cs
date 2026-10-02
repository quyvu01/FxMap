using FxMap.Abstractions;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.Fluent;
using FxMap.Fluent.Builders;
using FxMap.Tests.UnitTests.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Fluent;

/// <summary>Collection rules that cannot work are rejected when the profile is built, with a message that names the problem.</summary>
public class CollectionValidationTests
{
    public class Item
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string ReadOnly { get; } = "";
        public string PrivateSet { get; private set; } = "";
    }

    public class Model
    {
        public string Mrn { get; set; } = "";
        public string Name { get; set; } = "";
        public List<Item> Items { get; set; } = [];
        public List<Item> Other { get; set; } = [];
        public Item[] Array { get; set; } = [];
        public List<Item> NoSetter { get; } = [];
        public List<Item> PrivateSetter { get; private set; } = [];
    }

    private sealed class P(Action<P> configure) : ProfileOf<Model>
    {
        protected override void Configure() => configure(this);

        public DistributedKeyRuleBuilder<Model> Key<TKey>() where TKey : IDistributedKey => UseDistributedKey<TKey>();
    }

    private static P Build(Action<P> configure) => new(configure);

    private static string Reason(Action<P> configure) =>
        Should.Throw<DistributedMapException.InvalidCollectionRule>(() => Build(configure)).Message;

    #region Valid shapes

    [Fact]
    public void A_well_formed_collection_is_accepted()
    {
        var profile = Build(p => p.Key<UserKey>().Of(x => x.Mrn)
            .Collection(x => x.Items, v => v.For(x => x.Name).For(x => x.Email, "Email"))
            .For(x => x.Name, "Name"));

        profile.RuleGroups.Single().Collections.ShouldHaveSingleItem();
    }

    [Fact]
    public void Several_collections_on_different_properties_are_accepted_even_across_groups()
    {
        Should.NotThrow(() => Build(p =>
        {
            p.Key<UserKey>().Of(x => x.Mrn).Collection(x => x.Items, v => v.For(x => x.Name));
            p.Key<ProvinceKey>().Of(x => x.Mrn).Collection(x => x.Other, v => v.For(x => x.Name));
            p.Key<CountryKey>().Of(x => x.Mrn).Collection(x => x.Array, v => v.For(x => x.Name));
        }));
    }

    [Fact]
    public void Two_plain_rules_on_the_same_property_are_still_allowed_as_before()
    {
        Should.NotThrow(() => Build(p =>
        {
            p.Key<UserKey>().Of(x => x.Mrn).For(x => x.Name, "Name");
            p.Key<ProvinceKey>().Of(x => x.Mrn).For(x => x.Name, "Other");
        }));
    }

    #endregion

    #region Rejected

    [Fact]
    public void The_same_property_cannot_be_the_target_of_two_collection_rules_in_one_group()
    {
        var message = Reason(p => p.Key<UserKey>().Of(x => x.Mrn)
            .Collection(x => x.Items, v => v.For(x => x.Name))
            .Collection(x => x.Items, v => v.For(x => x.Email, "Email")));

        message.ShouldContain("Model.Items");
        message.ShouldContain("more than one Collection rule");
    }

    [Fact]
    public void The_same_property_cannot_be_the_target_of_collection_rules_in_two_groups()
    {
        Reason(p =>
        {
            p.Key<UserKey>().Of(x => x.Mrn).Collection(x => x.Items, v => v.For(x => x.Name));
            p.Key<ProvinceKey>().Of(x => x.Mrn).Collection(x => x.Items, v => v.For(x => x.Name));
        }).ShouldContain("more than one Collection rule");
    }

    [Fact]
    public void A_property_cannot_be_both_a_plain_target_and_a_collection_target()
    {
        var forFirst = Reason(p =>
        {
            p.Key<UserKey>().Of(x => x.Mrn).For(x => x.Items, "Items");
            p.Key<ProvinceKey>().Of(x => x.Mrn).Collection(x => x.Items, v => v.For(x => x.Name));
        });
        var collectionFirst = Reason(p =>
        {
            p.Key<ProvinceKey>().Of(x => x.Mrn).Collection(x => x.Items, v => v.For(x => x.Name));
            p.Key<UserKey>().Of(x => x.Mrn).For(x => x.Items, "Items");
        });

        forFirst.ShouldContain("also the target of a For");
        collectionFirst.ShouldContain("also the target of a For");
    }

    [Fact]
    public void A_collection_cannot_be_the_selector_of_a_key()
    {
        var message = Reason(p =>
        {
            p.Key<UserKey>().Of(x => x.Mrn).Collection(x => x.Items, v => v.For(x => x.Name));
            p.Key<ProvinceKey>().Of(x => x.Items).For(x => x.Name);
        });

        message.ShouldContain("selector");
        message.ShouldContain("Model.Items");
    }

    [Theory]
    [InlineData("NoSetter")]
    [InlineData("PrivateSetter")]
    public void The_collection_property_needs_a_public_setter(string property)
    {
        var message = Reason(p =>
        {
            var key = p.Key<UserKey>().Of(x => x.Mrn);
            if (property == "NoSetter") key.Collection(x => x.NoSetter, v => v.For(x => x.Name));
            else key.Collection(x => x.PrivateSetter, v => v.For(x => x.Name));
        });

        message.ShouldContain($"Model.{property}");
        message.ShouldContain("public setter");
    }

    [Fact]
    public void A_collection_without_item_rules_is_rejected()
    {
        var message = Reason(p => p.Key<UserKey>().Of(x => x.Mrn).Collection(x => x.Items, v => v.Limit(3)));

        message.ShouldContain("nothing to fill");
        message.ShouldContain("Item");
    }

    [Fact]
    public void Two_item_rules_cannot_fill_the_same_item_property()
    {
        var message = Reason(p => p.Key<UserKey>().Of(x => x.Mrn)
            .Collection(x => x.Items, v => v.For(x => x.Name, "A").For(x => x.Name, "B")));

        message.ShouldContain("Item.Name");
        message.ShouldContain("more than one For");
    }

    [Theory]
    [InlineData("ReadOnly")]
    [InlineData("PrivateSet")]
    public void An_item_property_needs_a_public_setter(string property)
    {
        var message = Reason(p => p.Key<UserKey>().Of(x => x.Mrn).Collection(x => x.Items, v =>
        {
            if (property == "ReadOnly") v.For(x => x.ReadOnly);
            else v.For(x => x.PrivateSet);
        }));

        message.ShouldContain($"Item.{property}");
        message.ShouldContain("no public setter");
    }

    #endregion

    #region Where the error surfaces

    internal sealed class InvalidProfile : ProfileOf<Model>
    {
        protected override void Configure() =>
            UseDistributedKey<UserKey>().Of(x => x.Mrn).Collection(x => x.Items, v => v.Limit(1));
    }

    internal sealed class DuplicatedExposedNameConfig : EntityConfigureOf<Item>
    {
        protected override void Configure()
        {
            Id(x => x.Name);
            ExposedName(x => x.Email, "Same");
            ExposedName(x => x.PrivateSet, "Same");
            UseDistributedKey<UserKey>();
        }
    }

    [Fact]
    public void The_real_error_is_reported_at_startup_not_a_reflection_wrapper()
    {
        var services = new ServiceCollection();

        Should.Throw<DistributedMapException.InvalidCollectionRule>(() =>
            services.AddFxMap(cfg => cfg.AddProfileConfigs(typeof(InvalidProfile))));
    }

    [Fact]
    public void The_same_goes_for_errors_thrown_by_an_entity_config()
    {
        var services = new ServiceCollection();

        Should.Throw<DistributedMapException.DuplicatedNameByExposedName>(() =>
            services.AddFxMap(cfg => cfg.AddEntityConfigs(typeof(DuplicatedExposedNameConfig))));
    }

    #endregion
}
