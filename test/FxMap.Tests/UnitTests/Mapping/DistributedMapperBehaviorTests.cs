using System.Text.Json;
using FxMap.Exceptions;
using Shouldly;
using Xunit;

namespace FxMap.Tests.UnitTests.Mapping;

/// <summary>
/// Characterization tests for DistributedMapper.MapDataAsync: scalar mapping, selectors, request shape,
/// dependency chains, multi level mapping, conditional expressions and error handling.
/// They describe CURRENT behavior so refactors of the mapper internals can be checked against them.
/// </summary>
public class DistributedMapperBehaviorTests
{
    #region Scalars

    [Fact]
    public async Task Maps_every_scalar_type_through_json()
    {
        var created = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        using var h = MappingHarness.Create(remote: r => r.Setup<UserKey>((id, e) => e switch
        {
            "Name" => "Ann",
            "Email" => "ann@x.com",
            "Age" => 41,
            "Balance" => 12.5m,
            "CreatedAt" => created,
            "Active" => true,
            "Role" => Role.Admin,
            "Score" => 7,
            _ => FakeRemote.Absent
        }));
        var dto = new FlatDto { UserId = "u1" };

        await h.Map(dto);

        dto.UserName.ShouldBe("Ann");
        dto.UserEmail.ShouldBe("ann@x.com");
        dto.Age.ShouldBe(41);
        dto.Balance.ShouldBe(12.5m);
        dto.CreatedAt.ShouldBe(created);
        dto.Active.ShouldBeTrue();
        dto.Role.ShouldBe(Role.Admin);
        dto.Score.ShouldBe(7);
        dto.Untouched.ShouldBe("keep");
    }

    [Fact]
    public async Task Json_null_overwrites_the_target_with_null()
    {
        using var h = MappingHarness.Create(remote: r => r.Setup<UserKey>((_, e) => e == "Score" ? null : "x"));
        var dto = new FlatDto { UserId = "u1", Score = 99 };

        await h.Map(dto);

        dto.Score.ShouldBeNull();
    }

    [Fact]
    public async Task Expression_missing_in_response_leaves_target_untouched()
    {
        using var h = MappingHarness.Create(remote: r =>
            r.Setup<UserKey>((_, e) => e == "Name" ? "Ann" : FakeRemote.Absent));
        var dto = new FlatDto { UserId = "u1", UserEmail = "old@x.com", Age = 5 };

        await h.Map(dto);

        dto.UserName.ShouldBe("Ann");
        dto.UserEmail.ShouldBe("old@x.com");
        dto.Age.ShouldBe(5);
    }

    [Fact]
    public async Task Unknown_id_leaves_the_object_untouched()
    {
        using var h = MappingHarness.Create(remote: r =>
            r.Setup<UserKey>(MappingHarness.Standard("user"), id => id != "ghost"));
        var ghost = new FlatDto { UserId = "ghost", UserName = "before" };
        var real = new FlatDto { UserId = "u1" };

        await h.Map(new List<FlatDto> { ghost, real });

        ghost.UserName.ShouldBe("before");
        real.UserName.ShouldBe("user-name:u1");
    }

    [Fact]
    public async Task Null_selector_is_skipped_and_other_objects_still_map()
    {
        using var h = MappingHarness.Create();
        var withoutId = new FlatDto { UserId = null, UserName = "before" };
        var withId = new FlatDto { UserId = "u1" };

        await h.Map(new[] { withoutId, withId });

        withoutId.UserName.ShouldBe("before");
        withId.UserName.ShouldBe("user-name:u1");
        h.Remote.NonEmptyCallsOf<UserKey>().ShouldHaveSingleItem().Ids.ShouldBe(["u1"]);
    }

    [Fact]
    public async Task Guid_selector_is_sent_as_its_string_form()
    {
        using var h = MappingHarness.Create();
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var dto = new GuidSelectorDto { UserId = id };

        await h.Map(dto);

        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().Ids.ShouldBe([id.ToString()]);
        dto.UserName.ShouldBe($"user-name:{id}");
    }

    [Fact]
    public async Task Int_selector_is_sent_as_its_string_form()
    {
        using var h = MappingHarness.Create();
        var dto = new IntSelectorDto { UserId = 42 };

        await h.Map(dto);

        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().Ids.ShouldBe(["42"]);
        dto.UserName.ShouldBe("user-name:42");
    }

    [Fact]
    public async Task Rule_without_expression_forwards_a_null_expression()
    {
        using var h = MappingHarness.Create(remote: r =>
            r.Setup<UserKey>((id, e) => e is null ? $"default:{id}" : FakeRemote.Absent));
        var dto = new NullExpressionDto { UserId = "u1" };

        await h.Map(dto);

        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().Expressions.ShouldBe([null]);
        dto.UserName.ShouldBe("default:u1");
    }

    #endregion

    #region Request shape

    [Fact]
    public async Task Request_ids_and_expressions_are_deduplicated_across_many_objects()
    {
        using var h = MappingHarness.Create();
        var dtos = Enumerable.Range(0, 500).Select(i => new FlatDto { UserId = $"u{i % 7}" }).ToList();

        await h.Map(dtos);

        var call = h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem();
        call.Ids.OrderBy(x => x).ShouldBe(Enumerable.Range(0, 7).Select(i => $"u{i}").OrderBy(x => x));
        call.Expressions.OrderBy(x => x)
            .ShouldBe(["Active", "Age", "Balance", "CreatedAt", "Email", "Name", "Role", "Score"]);
        dtos.ShouldAllBe(d => d.UserName == $"user-name:{d.UserId}");
    }

    [Fact]
    public async Task Same_id_on_many_objects_maps_each_of_them()
    {
        using var h = MappingHarness.Create();
        var dtos = Enumerable.Range(0, 20).Select(_ => new FlatDto { UserId = "same" }).ToList();

        await h.Map(dtos);

        dtos.ShouldAllBe(d => d.UserName == "user-name:same" && d.UserEmail == "user-email:same");
    }

    [Fact]
    public async Task Two_selectors_of_the_same_key_share_one_request_and_each_picks_its_own_expression()
    {
        using var h = MappingHarness.Create();
        var dto = new TwoSelectorsDto { PrimaryUserId = "a", SecondaryUserId = "b" };

        await h.Map(dto);

        var call = h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem();
        call.Ids.OrderBy(x => x).ShouldBe(["a", "b"]);
        call.Expressions.OrderBy(x => x).ShouldBe(["Email", "Name"]);
        dto.PrimaryUserName.ShouldBe("user-name:a");
        dto.SecondaryUserEmail.ShouldBe("user-email:b");
    }

    [Fact]
    public async Task Fetch_drops_null_ids_and_duplicates()
    {
        using var h = MappingHarness.Create();

        var response = await h.Mapper.FetchDataAsync<UserKey>(
            new Models.DistributedMapRequest(["a", null, "a", "b"], ["Name", "Name"]));

        var call = h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem();
        call.Ids.OrderBy(x => x).ShouldBe(["a", "b"]);
        call.Expressions.ShouldBe(["Name"]);
        response.Items.Select(i => i.Id).OrderBy(x => x).ShouldBe(["a", "b"]);
    }

    #endregion

    #region Dependency chains

    [Fact]
    public async Task Chain_resolves_level_by_level_and_feeds_later_ids_from_earlier_results()
    {
        using var h = MappingHarness.Create();
        var dto = new ChainDto { UserId = "u1" };

        await h.Map(dto);

        dto.UserName.ShouldBe("user-name:u1");
        dto.ProvinceId.ShouldBe("province-id:u1");
        dto.ProvinceName.ShouldBe("province-name:province-id:u1");
        dto.CountryId.ShouldBe("country-id:province-id:u1");
        dto.CountryName.ShouldBe("country-name:country-id:province-id:u1");

        h.Remote.CallsOf<ProvinceKey>().ShouldHaveSingleItem().Ids.ShouldBe(["province-id:u1"]);
        h.Remote.CallsOf<CountryKey>().ShouldHaveSingleItem().Ids.ShouldBe(["country-id:province-id:u1"]);
    }

    [Fact]
    public async Task Chain_levels_never_overlap_in_time()
    {
        using var h = MappingHarness.Create(remote: r => r.OnRequest = _ => Task.Delay(15));
        var dto = new ChainDto { UserId = "u1" };

        await h.Map(dto);

        var user = h.Remote.CallsOf<UserKey>().Single();
        var province = h.Remote.CallsOf<ProvinceKey>().Single();
        var country = h.Remote.CallsOf<CountryKey>().Single();
        province.StartTs.ShouldBeGreaterThanOrEqualTo(user.EndTs);
        country.StartTs.ShouldBeGreaterThanOrEqualTo(province.EndTs);
    }

    [Fact]
    public async Task Chain_stops_when_an_intermediate_value_is_missing()
    {
        using var h = MappingHarness.Create(remote: r => r.Setup<UserKey>((_, e) =>
            e == "Name" ? "Ann" : FakeRemote.Absent));
        var dto = new ChainDto { UserId = "u1" };

        await h.Map(dto);

        dto.UserName.ShouldBe("Ann");
        dto.ProvinceId.ShouldBeNull();
        dto.ProvinceName.ShouldBeNull();
        dto.CountryName.ShouldBeNull();
        h.Remote.NonEmptyCallsOf<ProvinceKey>().ShouldBeEmpty();
        h.Remote.NonEmptyCallsOf<CountryKey>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Independent_keys_on_the_same_level_are_requested_concurrently()
    {
        var arrived = 0;
        var bothArrived = new TaskCompletionSource();
        using var h = MappingHarness.Create(c => c.ThrowIfException(), r => r.OnRequest = async key =>
        {
            if (key != typeof(UserKey) && key != typeof(ProductKey)) return;
            if (Interlocked.Increment(ref arrived) == 2) bothArrived.TrySetResult();
            await bothArrived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        });
        var dto = new DiamondDto { UserId = "u1", ProductId = "p1" };

        await h.Map(dto);

        dto.UserName.ShouldBe("user-name:u1");
        dto.ProductName.ShouldBe("product-name:p1");
        dto.ProvinceName.ShouldBe("province-name:province-id:u1");
        dto.CategoryName.ShouldBe("category-name:category-id:p1");
    }

    [Fact]
    public async Task Each_key_is_requested_once_per_level()
    {
        using var h = MappingHarness.Create();
        var dtos = Enumerable.Range(0, 50)
            .Select(i => new DiamondDto { UserId = $"u{i % 5}", ProductId = $"p{i % 3}" }).ToList();

        await h.Map(dtos);

        h.Remote.CallsOf<UserKey>().Count.ShouldBe(1);
        h.Remote.CallsOf<ProductKey>().Count.ShouldBe(1);
        h.Remote.CallsOf<ProvinceKey>().Count.ShouldBe(1);
        h.Remote.CallsOf<CategoryKey>().Count.ShouldBe(1);
        h.Remote.CallsOf<ProvinceKey>().Single().Ids.Length.ShouldBe(5);
        h.Remote.CallsOf<CategoryKey>().Single().Ids.Length.ShouldBe(3);
    }

    #endregion

    #region Multi level (mapped values that are objects)

    [Fact]
    public async Task Object_returned_by_remote_is_mapped_on_the_next_level()
    {
        using var h = MappingHarness.Create(remote: r => r.Setup<UserKey>((_, e) => e switch
        {
            "Address" => new AddressDto { ProvinceId = "p1" },
            "Addresses" => new List<AddressDto> { new() { ProvinceId = "p2" }, new() { ProvinceId = "p3" } },
            _ => FakeRemote.Absent
        }));
        var dto = new ProfileDto { UserId = "u1" };

        await h.Map(dto);

        dto.Address.ProvinceName.ShouldBe("province-name:p1");
        dto.Addresses.Select(a => a.ProvinceName).ShouldBe(["province-name:p2", "province-name:p3"]);
        var call = h.Remote.CallsOf<ProvinceKey>().ShouldHaveSingleItem();
        call.Ids.OrderBy(x => x).ShouldBe(["p1", "p2", "p3"]);
    }

    [Fact]
    public async Task Null_object_from_remote_ends_the_walk_without_error()
    {
        using var h = MappingHarness.Create(remote: r => r.Setup<UserKey>((_, _) => null));
        var dto = new ProfileDto { UserId = "u1" };

        await h.Map(dto);

        dto.Address.ShouldBeNull();
        dto.Addresses.ShouldBeNull();
        h.Remote.CallsOf<ProvinceKey>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Nesting_depth_too_low_throws_when_configured_to_throw()
    {
        using var h = MappingHarness.Create(c =>
        {
            c.SetMaxNestingDepth(1);
            c.ThrowIfException();
        }, r => r.Setup<UserKey>((_, e) => e == "Address" ? new AddressDto { ProvinceId = "p1" } : FakeRemote.Absent));

        await Should.ThrowAsync<DistributedMapException.MaxNestingDepthReached>(() =>
            h.Map(new ProfileDto { UserId = "u1" }));
    }

    [Fact]
    public async Task Nesting_depth_too_low_stops_silently_when_not_configured_to_throw()
    {
        using var h = MappingHarness.Create(c => c.SetMaxNestingDepth(1),
            r => r.Setup<UserKey>((_, e) => e == "Address" ? new AddressDto { ProvinceId = "p1" } : FakeRemote.Absent));
        var dto = new ProfileDto { UserId = "u1" };

        await h.Map(dto);

        dto.Address.ShouldNotBeNull();
        dto.Address.ProvinceId.ShouldBe("p1");
        dto.Address.ProvinceName.ShouldBeNull();
    }

    [Fact]
    public async Task Nesting_depth_is_not_consumed_when_no_further_level_is_needed()
    {
        using var h = MappingHarness.Create(c =>
        {
            c.SetMaxNestingDepth(1);
            c.ThrowIfException();
        });
        var dto = new FlatDto { UserId = "u1" };

        await h.Map(dto);

        dto.UserName.ShouldBe("user-name:u1");
    }

    [Fact]
    public async Task Nesting_depth_that_is_just_enough_maps_everything()
    {
        using var h = MappingHarness.Create(c =>
        {
            c.SetMaxNestingDepth(2);
            c.ThrowIfException();
        }, r => r.Setup<UserKey>((_, e) => e == "Address" ? new AddressDto { ProvinceId = "p1" } : FakeRemote.Absent));
        var dto = new ProfileDto { UserId = "u1" };

        await h.Map(dto);

        dto.Address.ProvinceName.ShouldBe("province-name:p1");
    }

    #endregion

    #region Conditional expressions

    [Fact]
    public async Task Conditional_expression_picks_the_branch_for_the_current_call()
    {
        using var h = MappingHarness.Create();

        h.Mode.UseEmail = false;
        var first = new ConditionalDto { UserId = "u1" };
        await h.Map(first);

        h.Mode.UseEmail = true;
        var second = new ConditionalDto { UserId = "u1" };
        await h.Map(second);

        first.UserName.ShouldBe("user-name:u1");
        second.UserName.ShouldBe("user-email:u1");
    }

    [Fact]
    public async Task Conditional_expression_is_resolved_for_every_object_in_one_call()
    {
        using var h = MappingHarness.Create();
        h.Mode.UseEmail = true;
        var dtos = Enumerable.Range(0, 10).Select(i => new ConditionalDto { UserId = $"u{i}" }).ToList();

        await h.Map(dtos);

        dtos.ShouldAllBe(d => d.UserName == $"user-email:{d.UserId}");
        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().Expressions.ShouldBe(["Email"]);
    }

    #endregion

    #region Errors

    [Fact]
    public async Task Malformed_value_is_swallowed_by_default_and_other_properties_still_map()
    {
        using var h = MappingHarness.Create(remote: r => r.Setup<UserKey>((_, e) => e switch
        {
            "Age" => new RawJson("\"not a number\""),
            "Name" => "Ann",
            _ => FakeRemote.Absent
        }));
        var dto = new FlatDto { UserId = "u1", Age = 3 };

        await h.Map(dto);

        dto.Age.ShouldBe(3);
        dto.UserName.ShouldBe("Ann");
    }

    [Fact]
    public async Task Malformed_value_throws_when_configured_to_throw()
    {
        using var h = MappingHarness.Create(c => c.ThrowIfException(), r => r.Setup<UserKey>((_, e) =>
            e == "Age" ? new RawJson("\"not a number\"") : FakeRemote.Absent));

        await Should.ThrowAsync<JsonException>(() => h.Map(new FlatDto { UserId = "u1" }));
    }

    [Fact]
    public async Task Remote_failure_is_swallowed_by_default()
    {
        using var h = MappingHarness.Create(remote: r =>
            r.OnRequest = _ => throw new InvalidOperationException("remote is down"));
        var dto = new FlatDto { UserId = "u1", UserName = "before" };

        await h.Map(dto);

        dto.UserName.ShouldBe("before");
    }

    [Fact]
    public async Task Remote_failure_surfaces_when_configured_to_throw()
    {
        using var h = MappingHarness.Create(c => c.ThrowIfException(), r =>
            r.OnRequest = _ => throw new InvalidOperationException("remote is down"));

        await Should.ThrowAsync<InvalidOperationException>(() => h.Map(new FlatDto { UserId = "u1" }));
    }

    [Fact]
    public async Task One_key_failing_does_not_block_the_other_keys_on_the_same_level()
    {
        using var h = MappingHarness.Create(remote: r => r.OnRequest = key =>
            key == typeof(UserKey) ? throw new InvalidOperationException("user service down") : Task.CompletedTask);
        var dto = new DiamondDto { UserId = "u1", ProductId = "p1" };

        await h.Map(dto);

        dto.UserName.ShouldBeNull();
        dto.ProductName.ShouldBe("product-name:p1");
        dto.CategoryName.ShouldBe("category-name:category-id:p1");
    }

    [Fact]
    public async Task Cancellation_token_reaches_the_remote_request_but_does_not_abort_the_mapping_by_itself()
    {
        using var h = MappingHarness.Create();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var dto = new FlatDto { UserId = "u1" };

        await h.Map(dto, cts.Token);

        h.Remote.CallsOf<UserKey>().ShouldHaveSingleItem().CancellationRequested.ShouldBeTrue();
        dto.UserName.ShouldBe("user-name:u1");
    }

    #endregion
}
