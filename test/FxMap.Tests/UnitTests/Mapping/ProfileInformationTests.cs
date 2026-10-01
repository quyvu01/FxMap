using System.Reflection;
using FxMap.Accessors.PropertyAccessors;
using FxMap.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>
/// Pins down what a profile publishes (Accessors / DependencyGraphs / GetInformation). The mapper's hot path reads
/// these on every object, so any caching or restructuring of them must keep these answers identical.
/// </summary>
public class ProfileInformationTests
{
    private static PropertyInfo Prop<T>(string name) => typeof(T).GetProperty(name)!;

    #region Chain profile

    private static readonly ChainDtoProfile Chain = new();

    [Theory]
    [InlineData("UserName", 0, "Name", typeof(UserKey), "UserId")]
    [InlineData("ProvinceId", 0, "ProvinceId", typeof(UserKey), "UserId")]
    [InlineData("ProvinceName", 1, "Name", typeof(ProvinceKey), "ProvinceId")]
    [InlineData("CountryId", 1, "CountryId", typeof(ProvinceKey), "ProvinceId")]
    [InlineData("CountryName", 2, "Name", typeof(CountryKey), "CountryId")]
    public void Target_properties_report_order_expression_key_and_selector(string property, int order,
        string expression, Type key, string selector)
    {
        var info = Chain.GetInformation(Prop<ChainDto>(property));

        info.Order.ShouldBe(order);
        info.Expression.ShouldBe(expression);
        info.RuntimeDistributedKeyType.ShouldBe(key);
        info.RequiredAccessor.ShouldBeSameAs(Chain.Accessors[Prop<ChainDto>(selector)]);
    }

    [Theory]
    [InlineData("UserId")]
    [InlineData("Id")]
    public void Properties_that_are_not_targets_report_the_empty_information(string property)
    {
        var info = Chain.GetInformation(Prop<ChainDto>(property));

        info.Order.ShouldBe(0);
        info.Expression.ShouldBeNull();
        info.RuntimeDistributedKeyType.ShouldBeNull();
        info.RequiredAccessor.ShouldBeNull();
    }

    [Fact]
    public void Information_is_stable_across_calls()
    {
        var property = Prop<ChainDto>("CountryName");

        var first = Chain.GetInformation(property);
        var second = Chain.GetInformation(property);

        second.ShouldBe(first);
        second.RequiredAccessor.ShouldBeSameAs(first.RequiredAccessor);
    }

    [Fact]
    public void Dependency_chain_lists_the_target_first_and_the_root_selector_last()
    {
        var chain = Chain.DependencyGraphs[Prop<ChainDto>("CountryName")];

        chain.Select(c => c.TargetPropertyInfo.Name).ShouldBe(["CountryName", "CountryId", "ProvinceId"]);
        chain.Select(c => c.RequiredPropertyInfo.Name).ShouldBe(["CountryId", "ProvinceId", "UserId"]);
    }

    [Fact]
    public void Accessors_contain_targets_and_selectors_only_when_there_is_no_other_non_primitive_property()
    {
        Chain.Accessors.Keys.Select(p => p.Name).OrderBy(x => x).ShouldBe(
        [
            "CountryId", "CountryName", "ProvinceId", "ProvinceName", "UserId", "UserName"
        ]);
    }

    [Fact]
    public void Dependency_graph_has_an_entry_for_each_target_property_only()
    {
        Chain.DependencyGraphs.Keys.Select(p => p.Name).OrderBy(x => x).ShouldBe(
            ["CountryId", "CountryName", "ProvinceId", "ProvinceName", "UserName"]);
    }

    #endregion

    #region Accessors of container-style profiles

    [Fact]
    public void Non_primitive_properties_get_accessors_even_without_rules()
    {
        var profile = new OrderDtoProfile();

        profile.Accessors.Keys.Select(p => p.Name).OrderBy(x => x).ShouldBe(
            ["Array", "ByCode", "Items", "Lazy", "Main", "UserId", "UserName"]);
        profile.GetInformation(Prop<OrderDto>("Items")).RequiredAccessor.ShouldBeNull();
        profile.GetInformation(Prop<OrderDto>("Id")).RequiredAccessor.ShouldBeNull();
    }

    [Fact]
    public void Accessor_reads_and_writes_the_property()
    {
        var profile = new FlatDtoProfile();
        var dto = new FlatDto { UserId = "u1" };
        var accessor = profile.Accessors[Prop<FlatDto>("UserName")];

        accessor.Set(dto, "Ann");

        accessor.Get(dto).ShouldBe("Ann");
        dto.UserName.ShouldBe("Ann");
    }

    [Fact]
    public void Value_type_selectors_are_read_boxed()
    {
        var profile = new GuidSelectorDtoProfile();
        var id = Guid.NewGuid();

        var selector = profile.GetInformation(Prop<GuidSelectorDto>("UserName")).RequiredAccessor;

        selector.Get(new GuidSelectorDto { UserId = id }).ShouldBe(id);
    }

    [Fact]
    public void Virtual_profile_exposes_only_non_primitive_properties()
    {
        var profile = new VirtualProfileOf<HolderDto>();

        profile.Accessors.Keys.Select(p => p.Name).OrderBy(x => x).ShouldBe(["Boxed", "Item", "Items"]);
        profile.DependencyGraphs.ShouldBeEmpty();
        profile.GetInformation(Prop<HolderDto>("Item")).Order.ShouldBe(0);
        profile.GetInformation(Prop<HolderDto>("Item")).RequiredAccessor.ShouldBeNull();
    }

    #endregion

    #region Conditional expressions

    [Fact]
    public async Task Conditional_expression_is_resolved_on_every_call_never_cached()
    {
        var profile = new ConditionalDtoProfile();
        var mode = new ModeFlag();
        var sp = new ServiceCollection().AddSingleton(mode).BuildServiceProvider();
        var info = profile.GetInformation(Prop<ConditionalDto>("UserName"));

        mode.UseEmail = false;
        (await info.ResolveExpression(sp, CancellationToken.None)).ShouldBe("Name");
        mode.UseEmail = true;
        (await info.ResolveExpression(sp, CancellationToken.None)).ShouldBe("Email");
        mode.UseEmail = false;
        (await info.ResolveExpression(sp, CancellationToken.None)).ShouldBe("Name");
    }

    [Fact]
    public async Task Plain_expression_resolves_to_itself_without_needing_a_service_provider()
    {
        var info = Chain.GetInformation(Prop<ChainDto>("UserName"));

        (await info.ResolveExpression(null, CancellationToken.None)).ShouldBe("Name");
    }

    #endregion
}
