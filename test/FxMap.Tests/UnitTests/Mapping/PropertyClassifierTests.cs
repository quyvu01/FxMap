using System.Collections.ObjectModel;
using System.Reflection;
using FxMap.Fluent;
using FxMap.Helpers;
using FxMap.Models;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>
/// The classifier decides what the walker may skip. The rule is "skip only what provably cannot hold a model",
/// so every case that CAN hold one must stay Walkable.
/// </summary>
public class PropertyClassifierTests
{
    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(string))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(TimeSpan))]
    [InlineData(typeof(Role))]
    [InlineData(typeof(KeyValuePair<string, ItemDto>))]
    public void Value_types_and_strings_are_leaves(Type type) =>
        PropertyClassifier.Classify(type).ShouldBe(TypeKind.Leaf);

    [Theory]
    [InlineData(typeof(byte[]))]
    [InlineData(typeof(int[]))]
    [InlineData(typeof(string[]))]
    [InlineData(typeof(int?[]))]
    [InlineData(typeof(Guid[]))]
    [InlineData(typeof(string[][]))]
    [InlineData(typeof(List<string>))]
    [InlineData(typeof(List<DateTime>))]
    [InlineData(typeof(IEnumerable<string>))]
    [InlineData(typeof(ICollection<int>))]
    [InlineData(typeof(IList<Guid>))]
    [InlineData(typeof(IReadOnlyList<decimal>))]
    [InlineData(typeof(HashSet<string>))]
    [InlineData(typeof(ReadOnlyCollection<int>))]
    [InlineData(typeof(Dictionary<string, string>))]
    [InlineData(typeof(Dictionary<int, decimal>))]
    [InlineData(typeof(IDictionary<string, int>))]
    [InlineData(typeof(IReadOnlyDictionary<string, Guid>))]
    [InlineData(typeof(List<List<string>>))]
    [InlineData(typeof(Dictionary<string, string[]>))]
    [InlineData(typeof(Dictionary<string, List<int>>))]
    [InlineData(typeof(CountingStrings))]
    public void Collections_of_leaves_are_leaf_collections(Type type) =>
        PropertyClassifier.Classify(type).ShouldBe(TypeKind.LeafCollection);

    [Theory]
    [InlineData(typeof(Uri))]
    [InlineData(typeof(Version))]
    [InlineData(typeof(Type))]
    [InlineData(typeof(MethodInfo))]
    [InlineData(typeof(Assembly))]
    [InlineData(typeof(Action))]
    [InlineData(typeof(Func<int, int>))]
    [InlineData(typeof(Stream))]
    [InlineData(typeof(MemoryStream))]
    [InlineData(typeof(System.Globalization.CultureInfo))]
    [InlineData(typeof(System.Text.Encoding))]
    public void Framework_types_that_never_hold_models_are_opaque(Type type) =>
        PropertyClassifier.Classify(type).ShouldBe(TypeKind.Opaque);

    [Theory]
    [InlineData(typeof(ItemDto))]
    [InlineData(typeof(OrderDto))]
    [InlineData(typeof(object))]
    [InlineData(typeof(BaseItem))]
    [InlineData(typeof(IEnumerable<object>))]
    [InlineData(typeof(IEnumerable<ItemDto>))]
    [InlineData(typeof(List<ItemDto>))]
    [InlineData(typeof(ItemDto[]))]
    [InlineData(typeof(ItemDto[][]))]
    [InlineData(typeof(List<List<ItemDto>>))]
    [InlineData(typeof(Dictionary<string, ItemDto>))]
    [InlineData(typeof(Dictionary<string, object>))]
    [InlineData(typeof(Dictionary<string, List<ItemDto>>))]
    [InlineData(typeof(IReadOnlyDictionary<string, ItemDto>))]
    [InlineData(typeof(System.Collections.IEnumerable))]
    [InlineData(typeof(System.Collections.ArrayList))]
    [InlineData(typeof(System.Collections.IDictionary))]
    [InlineData(typeof(Array))]
    [InlineData(typeof(IComparable))]
    [InlineData(typeof(Task<ItemDto>))]
    public void Anything_that_can_hold_a_model_stays_walkable(Type type) =>
        PropertyClassifier.Classify(type).ShouldBe(TypeKind.Walkable);

    [Fact]
    public void A_collection_with_two_different_item_types_is_walkable_because_the_walker_cannot_know_which_one()
    {
        PropertyClassifier.Classify(typeof(TwoItemTypes)).ShouldBe(TypeKind.Walkable);
    }

    [Fact]
    public void Self_referencing_generic_shapes_terminate()
    {
        PropertyClassifier.Classify(typeof(SelfList)).ShouldBe(TypeKind.Walkable);
    }

    [Fact]
    public void Classification_is_stable_and_cached()
    {
        PropertyClassifier.Classify(typeof(List<string>)).ShouldBe(PropertyClassifier.Classify(typeof(List<string>)));
        PropertyClassifier.ShouldWalk(typeof(ItemDto)).ShouldBeTrue();
        PropertyClassifier.ShouldWalk(typeof(byte[])).ShouldBeFalse();
    }

    [Fact]
    public void Profile_without_walkable_properties_has_no_walk_entries_and_no_extra_accessors()
    {
        var profile = new BlobDtoProfile();

        profile.Accessors.Keys.Select(p => p.Name).OrderBy(x => x).ShouldBe(["UserId", "UserName"]);
        var plan = ((IProfilePlanSource)profile).Plan;
        plan.WalkEntries.ShouldBeEmpty();
        plan.RuleEntries.Select(e => e.Property.Name).ShouldBe(["UserName"]);
    }

    [Fact]
    public void Profile_keeps_accessors_of_every_walkable_collection()
    {
        var profile = new VirtualProfileOf<CollectionsDto>();

        profile.Accessors.Keys.Select(p => p.Name).OrderBy(x => x).ShouldBe(
            ["AsInterface", "Bag", "Boxed", "Grouped", "Jagged", "Matrix", "Mixed", "ReadOnly"]);
    }

    private sealed class TwoItemTypes : IEnumerable<string>, IEnumerable<int>
    {
        IEnumerator<string> IEnumerable<string>.GetEnumerator() => throw new NotSupportedException();
        IEnumerator<int> IEnumerable<int>.GetEnumerator() => throw new NotSupportedException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new NotSupportedException();
    }

    private sealed class SelfList : List<SelfList>;
}
