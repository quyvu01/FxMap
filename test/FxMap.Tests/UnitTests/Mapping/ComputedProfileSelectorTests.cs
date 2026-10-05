using FxMap.Exceptions;
using FxMap.Fluent;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>Customer id made of two properties: <c>Of(x => x.Mrn + x.Code)</c>.</summary>
public class ComputedKeyDto
{
    public string Mrn { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

internal sealed class ComputedKeyDtoProfile : ProfileOf<ComputedKeyDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.Mrn + x.Code).For(x => x.Name, "Name").For(x => x.Email, "Email");
}

/// <summary>The second key reads ProvinceId, which the first key fills: it must be mapped afterwards.</summary>
public class ComputedChainDto
{
    public string UserId { get; set; }
    public string Suffix { get; set; }
    public string ProvinceId { get; set; }
    public string ProvinceName { get; set; }
}

internal sealed class ComputedChainDtoProfile : ProfileOf<ComputedChainDto>
{
    protected override void Configure()
    {
        UseDistributedKey<UserKey>().Of(x => x.UserId).For(x => x.ProvinceId, "ProvinceId");
        UseDistributedKey<ProvinceKey>().Of(x => x.ProvinceId + x.Suffix).For(x => x.ProvinceName, "Name");
    }
}

/// <summary>The key reads Email, and the same group overwrites Email with the value of the remote.</summary>
public class ComputedSelfReadDto
{
    public string Id { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
}

internal sealed class ComputedSelfReadDtoProfile : ProfileOf<ComputedSelfReadDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.Email.Length > 0 ? x.Id + x.Email : x.Id)
            .For(x => x.Email, "Email").For(x => x.Name, "Name");
}

/// <summary>The key goes through a nested object that can be null.</summary>
public class ComputedNestedDto
{
    public class Owner
    {
        public string Code { get; set; }
    }

    public Owner Who { get; set; }
    public string Name { get; set; }
}

internal sealed class ComputedNestedDtoProfile : ProfileOf<ComputedNestedDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.Who.Code).For(x => x.Name, "Name");
}

public class ComputedProfileSelectorTests
{
    #region What the profile publishes

    [Fact]
    public void A_computed_key_reads_the_value_of_the_expression()
    {
        var profile = new ComputedKeyDtoProfile();
        var info = profile.GetInformation(typeof(ComputedKeyDto).GetProperty("Name")!);

        info.Order.ShouldBe(0);
        info.RuntimeDistributedKeyType.ShouldBe(typeof(UserKey));
        info.RequiredAccessor.Get(new ComputedKeyDto { Mrn = "M1", Code = "A" }).ShouldBe("M1A");
        // Nothing but the properties of the DTO is registered as an accessor of a key.
        profile.Accessors.Keys.Select(k => k.Name).ShouldNotContain("MrnCode");
    }

    [Fact]
    public void A_computed_key_is_read_only()
    {
        var profile = new ComputedKeyDtoProfile();
        var accessor = profile.GetInformation(typeof(ComputedKeyDto).GetProperty("Name")!).RequiredAccessor;

        Should.Throw<InvalidOperationException>(() => accessor.Set(new ComputedKeyDto(), "x"));
    }

    [Fact]
    public void A_key_that_reads_a_mapped_property_of_another_group_is_mapped_after_it()
    {
        var profile = new ComputedChainDtoProfile();

        profile.GetInformation(typeof(ComputedChainDto).GetProperty("ProvinceId")!).Order.ShouldBe(0);
        profile.GetInformation(typeof(ComputedChainDto).GetProperty("ProvinceName")!).Order.ShouldBe(1);
    }

    [Fact]
    public void A_key_that_reads_a_property_filled_by_its_own_group_does_not_depend_on_it()
    {
        var profile = new ComputedSelfReadDtoProfile();

        profile.GetInformation(typeof(ComputedSelfReadDto).GetProperty("Email")!).Order.ShouldBe(0);
        profile.GetInformation(typeof(ComputedSelfReadDto).GetProperty("Name")!).Order.ShouldBe(0);
    }

    [Fact]
    public void A_computed_key_through_a_null_has_no_key()
    {
        var profile = new ComputedNestedDtoProfile();
        var accessor = profile.GetInformation(typeof(ComputedNestedDto).GetProperty("Name")!).RequiredAccessor;

        accessor.Get(new ComputedNestedDto { Who = null }).ShouldBeNull();
        accessor.Get(new ComputedNestedDto { Who = new ComputedNestedDto.Owner { Code = "k" } }).ShouldBe("k");
    }

    #endregion

    #region Validation

    private sealed class NoReadProfile<TScanGuard> : ProfileOf<ComputedKeyDto>
    {
        protected override void Configure() => UseDistributedKey<UserKey>().Of(x => "constant").For(x => x.Name);
    }

    [Fact]
    public void A_key_that_does_not_read_the_dto_is_rejected()
    {
        var error = Should.Throw<DistributedMapException.InvalidProfileSelector>(() => new NoReadProfile<int>());

        error.Message.ShouldContain("does not read the model");
    }

    #endregion

    #region Mapping

    [Fact]
    public async Task The_remote_receives_the_composed_key_and_the_answer_is_matched_back()
    {
        using var h = MappingHarness.Create();
        var items = new[]
        {
            new ComputedKeyDto { Mrn = "M1", Code = "A" },
            new ComputedKeyDto { Mrn = "M1", Code = "B" },
            new ComputedKeyDto { Mrn = "M2", Code = "A" }
        };

        await h.Map(items);

        var call = h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem();
        call.Ids.OrderBy(x => x).ShouldBe(["M1A", "M1B", "M2A"]);
        items.Select(x => x.Name).ShouldBe(["user-name:M1A", "user-name:M1B", "user-name:M2A"]);
        items.Select(x => x.Email).ShouldBe(["user-email:M1A", "user-email:M1B", "user-email:M2A"]);
    }

    [Fact]
    public async Task Objects_with_the_same_composed_key_share_one_id()
    {
        using var h = MappingHarness.Create();
        var items = new[]
        {
            new ComputedKeyDto { Mrn = "M1", Code = "A" },
            new ComputedKeyDto { Mrn = "M1", Code = "A" }
        };

        await h.Map(items);

        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().Ids.ShouldBe(["M1A"]);
        items.Select(x => x.Name).ShouldAllBe(x => x == "user-name:M1A");
    }

    [Fact]
    public async Task A_key_built_from_a_mapped_property_is_requested_in_a_later_round()
    {
        using var h = MappingHarness.Create();
        var item = new ComputedChainDto { UserId = "u1", Suffix = "-x" };

        await h.Map(item);

        item.ProvinceId.ShouldBe("province-id:u1");
        h.Remote.CallsOf<ProvinceKey>().ShouldHaveSingleItem().Ids.ShouldBe(["province-id:u1-x"]);
        item.ProvinceName.ShouldBe("province-name:province-id:u1-x");
    }

    [Fact]
    public async Task Overwriting_a_property_read_by_the_key_does_not_lose_the_other_values_of_the_group()
    {
        using var h = MappingHarness.Create();
        var item = new ComputedSelfReadDto { Id = "u1", Email = "old" };

        await h.Map(item);

        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().Ids.ShouldBe(["u1old"]);
        item.Email.ShouldBe("user-email:u1old");
        // The key of Name is the one that was sent, not the one recomputed after Email changed.
        item.Name.ShouldBe("user-name:u1old");
    }

    [Fact]
    public async Task An_object_whose_key_goes_through_a_null_is_left_alone()
    {
        using var h = MappingHarness.Create();
        var items = new[]
        {
            new ComputedNestedDto { Who = null },
            new ComputedNestedDto { Who = new ComputedNestedDto.Owner { Code = "k" } }
        };

        await h.Map(items);

        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().Ids.ShouldBe(["k"]);
        items[0].Name.ShouldBeNull();
        items[1].Name.ShouldBe("user-name:k");
    }

    #endregion
}
