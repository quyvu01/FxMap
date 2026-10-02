using FxMap.Fluent;
using FxMap.Models;
using FxMap.Tests.UnitTests.Mapping;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Fluent;

/// <summary>How a <c>Collection(...)</c> rule shows up in what the profile publishes (dependency graph, information, plan).</summary>
public class ProfileCollectionTests
{
    public class Visit
    {
        public DateTime Date { get; set; }
        public string Status { get; set; } = "";
    }

    public class PatientCard
    {
        public string UserId { get; set; } = "";
        public string Mrn { get; set; } = "";
        public string PatientName { get; set; } = "";
        public List<Visit> Visits { get; set; } = [];
        public Visit[] Allergies { get; set; } = [];
        public List<Visit> Untouched { get; set; } = [];
        public string Notes { get; set; } = "";
    }

    // UserKey -> Mrn (order 0); ProvinceKey stands in for "the visits key" -> Visits / Allergies (order 1)
    private sealed class ChainedProfile : ProfileOf<PatientCard>
    {
        protected override void Configure()
        {
            UseDistributedKey<UserKey>().Of(x => x.UserId).For(x => x.Mrn, "Mrn");
            UseDistributedKey<ProvinceKey>().Of(x => x.Mrn)
                .Collection(x => x.Visits, v => v.For(x => x.Date, "VisitDate").For(x => x.Status).Limit(5))
                .For(x => x.PatientName, "Name")
                .Collection(x => x.Allergies, v => v.For(x => x.Status, "Allergy"));
        }
    }

    // The selector is a plain property: the collection is at order 0.
    private sealed class DirectProfile : ProfileOf<PatientCard>
    {
        protected override void Configure() =>
            UseDistributedKey<ProvinceKey>().Of(x => x.Mrn)
                .Collection(x => x.Visits, v => v.For(x => x.Date, "VisitDate"));
    }

    private static readonly ChainedProfile Chained = new();
    private static System.Reflection.PropertyInfo Prop(string name) => typeof(PatientCard).GetProperty(name)!;

    [Fact]
    public void A_collection_property_is_a_target_of_the_dependency_graph()
    {
        Chained.DependencyGraphs.Keys.Select(p => p.Name).OrderBy(x => x)
            .ShouldBe(["Allergies", "Mrn", "PatientName", "Visits"]);
    }

    [Fact]
    public void Its_chain_ends_at_the_root_selector_like_any_other_target()
    {
        var chain = Chained.DependencyGraphs[Prop("Visits")];

        chain.Select(c => c.TargetPropertyInfo.Name).ShouldBe(["Visits", "Mrn"]);
        chain.Select(c => c.RequiredPropertyInfo.Name).ShouldBe(["Mrn", "UserId"]);
    }

    [Fact]
    public void Information_describes_the_collection_target()
    {
        var info = Chained.GetInformation(Prop("Visits"));

        info.Order.ShouldBe(1, "Mrn is itself filled by another rule");
        info.Expression.ShouldBeNull("the expressions belong to the item rules");
        info.RuntimeDistributedKeyType.ShouldBe(typeof(ProvinceKey));
        info.RequiredAccessor.ShouldBeSameAs(Chained.Accessors[Prop("Mrn")]);
        info.Collection.ShouldNotBeNull();
        info.Collection!.ItemType.ShouldBe(typeof(Visit));
        info.Collection.Limit.ShouldBe(5);
        info.Collection.Rules.Select(r => r.TargetPropertyName).ShouldBe(["Date", "Status"]);
    }

    [Fact]
    public void The_collection_in_the_information_is_the_rule_recorded_by_the_builder()
    {
        var recorded = Chained.RuleGroups.SelectMany(g => g.Collections).Single(c => c.TargetPropertyName == "Visits");

        Chained.GetInformation(Prop("Visits")).Collection.ShouldBeSameAs(recorded);
    }

    [Fact]
    public void Two_collections_of_one_group_each_keep_their_own_rule()
    {
        var visits = Chained.GetInformation(Prop("Visits")).Collection!;
        var allergies = Chained.GetInformation(Prop("Allergies")).Collection!;

        visits.ShouldNotBeSameAs(allergies);
        allergies.Rules.Single().Expression.ShouldBe("Allergy");
        Chained.GetInformation(Prop("Allergies")).Order.ShouldBe(1);
    }

    [Fact]
    public void Properties_that_are_not_collections_have_no_collection_rule()
    {
        Chained.GetInformation(Prop("PatientName")).Collection.ShouldBeNull();
        Chained.GetInformation(Prop("Mrn")).Collection.ShouldBeNull();
        Chained.GetInformation(Prop("Notes")).Collection.ShouldBeNull();
    }

    [Fact]
    public void With_a_plain_selector_the_collection_is_at_order_zero()
    {
        var profile = new DirectProfile();

        profile.GetInformation(Prop("Visits")).Order.ShouldBe(0);
        profile.DependencyGraphs[Prop("Visits")].ShouldHaveSingleItem();
    }

    [Fact]
    public void Information_of_a_collection_is_stable_across_calls()
    {
        var first = Chained.GetInformation(Prop("Visits"));

        Chained.GetInformation(Prop("Visits")).ShouldBeSameAs(first);
    }

    [Fact]
    public void The_collection_property_gets_an_accessor_that_reads_and_writes_it()
    {
        var card = new PatientCard();
        var accessor = Chained.Accessors[Prop("Visits")];

        accessor.Set(card, new List<Visit> { new() { Status = "x" } });

        ((List<Visit>)accessor.Get(card)).Single().Status.ShouldBe("x");
        card.Visits.Single().Status.ShouldBe("x");
    }

    [Fact]
    public void The_plan_treats_the_collection_as_a_rule_and_does_not_walk_into_it()
    {
        var plan = ((IProfilePlanSource)Chained).Plan;

        plan.RuleEntries.Select(e => e.Property.Name).OrderBy(x => x)
            .ShouldBe(["Allergies", "Mrn", "PatientName", "Visits"]);
        plan.RuleEntries.Single(e => e.Property.Name == "Visits").Information.Collection.ShouldNotBeNull();
        plan.WalkEntries.Select(e => e.Property.Name).ShouldNotContain("Visits");
        plan.WalkEntries.Select(e => e.Property.Name).ShouldNotContain("Allergies");
    }

    [Fact]
    public void A_collection_property_without_a_rule_is_still_walked()
    {
        var plan = ((IProfilePlanSource)Chained).Plan;

        plan.WalkEntries.Select(e => e.Property.Name).ShouldBe(["Untouched"]);
    }
}
