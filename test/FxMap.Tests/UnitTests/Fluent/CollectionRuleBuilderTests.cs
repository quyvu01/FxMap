using System.Collections.ObjectModel;
using FxMap.Exceptions;
using FxMap.Fluent;
using FxMap.Fluent.Rules;
using FxMap.Tests.UnitTests.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Fluent;

/// <summary>The fluent <c>Collection(...)</c> API: what it records on the rule group, and what it rejects.</summary>
public class CollectionRuleBuilderTests
{
    public class VisitItem
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime Date { get; set; }
    }

    public class Patient
    {
        public string Mrn { get; set; } = "";
        public string PatientName { get; set; } = "";
        public List<VisitItem> AsList { get; set; } = [];
        public VisitItem[] AsArray { get; set; } = [];
        public IEnumerable<VisitItem> AsEnumerable { get; set; } = [];
        public IList<VisitItem> AsIList { get; set; } = [];
        public ICollection<VisitItem> AsICollection { get; set; } = [];
        public IReadOnlyList<VisitItem> AsReadOnlyList { get; set; } = [];
        public IReadOnlyCollection<VisitItem> AsReadOnlyCollection { get; set; } = [];
        public HashSet<VisitItem> AsHashSet { get; set; } = [];
        public ReadOnlyCollection<VisitItem> AsReadOnlyCollectionClass { get; set; } = new([]);
        public List<VisitItem> Second { get; set; } = [];
    }

    private sealed class Profile(Action<PropertyRulesStarter> configure) : ProfileOf<Patient>
    {
        protected override void Configure() =>
            configure(new PropertyRulesStarter(UseDistributedKey<UserKey>().Of(x => x.Mrn)));
    }

    // Lets a test describe the rules with a lambda while the profile only exposes protected members.
    private sealed class PropertyRulesStarter(FxMap.Fluent.Builders.PropertyRuleBuilder<Patient> builder)
    {
        public FxMap.Fluent.Builders.PropertyRuleBuilder<Patient> Builder { get; } = builder;
    }

    private static KeyRuleGroup GroupOf(Action<FxMap.Fluent.Builders.PropertyRuleBuilder<Patient>> configure) =>
        new Profile(s => configure(s.Builder)).RuleGroups.Single();

    #region What is recorded

    [Fact]
    public void Records_the_target_property_the_item_type_and_the_item_rules()
    {
        var group = GroupOf(b => b.Collection(x => x.AsList, v => v
            .For(x => x.Name)
            .For(x => x.Email, "Email")));

        var rule = group.Collections.ShouldHaveSingleItem();
        rule.TargetPropertyName.ShouldBe(nameof(Patient.AsList));
        rule.TargetPropertyInfo.ShouldBe(typeof(Patient).GetProperty(nameof(Patient.AsList)));
        rule.ItemType.ShouldBe(typeof(VisitItem));
        rule.Rules.Select(r => r.TargetPropertyName).ShouldBe([nameof(VisitItem.Name), nameof(VisitItem.Email)]);
        rule.Rules.Select(r => r.Expression).ShouldBe([null, "Email"]);
        rule.Rules.ShouldAllBe(r => r.TargetPropertyInfo.DeclaringType == typeof(VisitItem));
    }

    [Fact]
    public void Item_rules_do_not_leak_into_the_rules_of_the_model()
    {
        var group = GroupOf(b => b
            .Collection(x => x.AsList, v => v.For(x => x.Name))
            .For(x => x.PatientName, "Name"));

        group.Rules.Select(r => r.TargetPropertyName).ShouldBe([nameof(Patient.PatientName)]);
        group.Collections.Single().Rules.Select(r => r.TargetPropertyName).ShouldBe([nameof(VisitItem.Name)]);
    }

    [Fact]
    public void Rules_of_the_model_can_come_before_and_after_the_collection()
    {
        var group = GroupOf(b => b
            .For(x => x.PatientName, "Name")
            .Collection(x => x.AsList, v => v.For(x => x.Name))
            .For(x => x.PatientName, "Email"));

        group.Rules.Select(r => r.Expression).ShouldBe(["Name", "Email"]);
        group.Collections.Count.ShouldBe(1);
    }

    [Fact]
    public void A_group_can_have_several_collections()
    {
        var group = GroupOf(b => b
            .Collection(x => x.AsList, v => v.For(x => x.Name))
            .Collection(x => x.Second, v => v.For(x => x.Email, "Email")));

        group.Collections.Select(c => c.TargetPropertyName).ShouldBe([nameof(Patient.AsList), nameof(Patient.Second)]);
        group.Collections[1].Rules.Single().Expression.ShouldBe("Email");
    }

    [Fact]
    public void An_item_rule_can_use_a_conditional_expression()
    {
        var group = GroupOf(b => b.Collection(x => x.AsList, v => v
            .For(x => x.Name, c => c.If(sp => sp.GetRequiredService<ModeFlag>().UseEmail)
                .Expression("Email").Else("Name"))));

        var rule = group.Collections.Single().Rules.Single();
        rule.ConditionalExpression.ShouldNotBeNull();
        rule.Expression.ShouldBeNull();
    }

    [Fact]
    public async Task The_conditional_expression_of_an_item_rule_is_resolved_when_asked()
    {
        var group = GroupOf(b => b.Collection(x => x.AsList, v => v
            .For(x => x.Name, c => c.If(sp => sp.GetRequiredService<ModeFlag>().UseEmail)
                .Expression("Email").Else("Name"))));
        var flag = new ModeFlag();
        var sp = new ServiceCollection().AddSingleton(flag).BuildServiceProvider();
        var conditional = group.Collections.Single().Rules.Single().ConditionalExpression!;

        (await conditional.ResolveAsync(sp)).ShouldBe("Name");
        flag.UseEmail = true;
        (await conditional.ResolveAsync(sp)).ShouldBe("Email");
    }

    #endregion

    #region Order and limit

    [Fact]
    public void Order_and_limit_default_to_none()
    {
        var rule = GroupOf(b => b.Collection(x => x.AsList, v => v.For(x => x.Name))).Collections.Single();

        rule.OrderBy.ShouldBeEmpty();
        rule.Limit.ShouldBeNull();
    }

    [Fact]
    public void Order_keys_are_kept_in_priority_order_with_their_direction()
    {
        var rule = GroupOf(b => b.Collection(x => x.AsList, v => v
            .For(x => x.Name)
            .OrderByDescending("VisitDate")
            .ThenBy("Id")
            .ThenByDescending("Status"))).Collections.Single();

        rule.OrderBy.ShouldBe([
            new CollectionOrder("VisitDate", true), new CollectionOrder("Id", false),
            new CollectionOrder("Status", true)
        ]);
    }

    [Fact]
    public void OrderBy_replaces_the_sort_configured_before()
    {
        var rule = GroupOf(b => b.Collection(x => x.AsList, v => v
            .For(x => x.Name)
            .OrderBy("A").ThenBy("B")
            .OrderByDescending("C"))).Collections.Single();

        rule.OrderBy.ShouldBe([new CollectionOrder("C", true)]);
    }

    [Fact]
    public void Limit_is_recorded()
    {
        var rule = GroupOf(b => b.Collection(x => x.AsList, v => v.For(x => x.Name).Limit(25))).Collections.Single();

        rule.Limit.ShouldBe(25);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Limit_must_be_positive(int limit) =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            GroupOf(b => b.Collection(x => x.AsList, v => v.Limit(limit))));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Order_property_name_is_required(string name) =>
        Should.Throw<ArgumentException>(() =>
            GroupOf(b => b.Collection(x => x.AsList, v => v.OrderBy(name!))));

    #endregion

    #region Property types

    [Fact]
    public void List_array_and_interface_properties_are_accepted()
    {
        var group = GroupOf(b => b
            .Collection(x => x.AsList, v => v.For(x => x.Name))
            .Collection(x => x.AsArray, v => v.For(x => x.Name))
            .Collection(x => x.AsEnumerable, v => v.For(x => x.Name))
            .Collection(x => x.AsIList, v => v.For(x => x.Name))
            .Collection(x => x.AsICollection, v => v.For(x => x.Name))
            .Collection(x => x.AsReadOnlyList, v => v.For(x => x.Name))
            .Collection(x => x.AsReadOnlyCollection, v => v.For(x => x.Name)));

        group.Collections.Select(c => c.TargetPropertyName).ShouldBe([
            nameof(Patient.AsList), nameof(Patient.AsArray), nameof(Patient.AsEnumerable), nameof(Patient.AsIList),
            nameof(Patient.AsICollection), nameof(Patient.AsReadOnlyList), nameof(Patient.AsReadOnlyCollection)
        ]);
        group.Collections.ShouldAllBe(c => c.ItemType == typeof(VisitItem));
    }

    [Fact]
    public void A_set_cannot_hold_the_collection()
    {
        var error = Should.Throw<DistributedMapException.CollectionPropertyTypeNotSupported>(() =>
            GroupOf(b => b.Collection(x => x.AsHashSet, v => v.For(x => x.Name))));

        error.Message.ShouldContain("Patient.AsHashSet");
        error.Message.ShouldContain("VisitItem");
    }

    [Fact]
    public void A_concrete_collection_class_that_a_list_cannot_be_assigned_to_is_rejected()
    {
        Should.Throw<DistributedMapException.CollectionPropertyTypeNotSupported>(() =>
            GroupOf(b => b.Collection(x => x.AsReadOnlyCollectionClass, v => v.For(x => x.Name))));
    }

    #endregion

    [Fact]
    public void The_profile_builds_with_a_collection_rule()
    {
        var profile = new Profile(s => s.Builder
            .Collection(x => x.AsList, v => v.For(x => x.Name).Limit(3))
            .For(x => x.PatientName, "Name"));

        profile.ClrType.ShouldBe(typeof(Patient));
        profile.RuleGroups.Single().Collections.Single().Limit.ShouldBe(3);
    }
}
