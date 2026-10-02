using Shouldly;
using Xunit;

namespace FxMap.Tests.IntegrationTests.EfCore.Complex;

/// <summary>
/// End to end: real EF Core InMemory handlers, mixed id types (int, Guid, string, long, Guid?), chains of different
/// length, aggregations, filters, projections, conditional expressions and objects that are mapped again.
/// Every expectation is computed from the seed data with plain LINQ, independent of FxMap and EF.
/// </summary>
[Collection("EfCore Sequential")]
public class ComplexMappingTests
{
    public static TheoryData<int, int> Sizes() => new()
    {
        { 1, 5 },
        { 2, 40 },
        { 3, 200 }
    };


    private static void AssertOrder(OrderView v, DomainData d, bool useEmail)
    {
        var order = d.Orders.Single(o => o.Id == v.Id);
        var customer = d.Customers.Single(c => c.Id == order.CustomerId);
        var city = d.Cities.Single(c => c.Id == customer.CityId);
        var province = d.Provinces.Single(p => p.Id == city.ProvinceId);
        var country = d.Countries.Single(c => c.Id == province.CountryId);
        var manager = customer.ManagerId is { } mid ? d.Customers.Single(c => c.Id == mid) : null;
        var customerOrders = d.Orders.Where(o => o.CustomerId == customer.Id).ToList();
        var label = $"order {v.Id}";

        v.CustomerName.ShouldBe(customer.Name, label);
        v.CustomerEmail.ShouldBe(customer.Email, label);
        v.CustomerTier.ShouldBe(customer.Tier, label);
        v.CustomerIsVip.ShouldBe(customer.IsVip, label);
        v.CustomerCreatedAt.ShouldBe(customer.CreatedAt, label);
        v.CustomerOrderCount.ShouldBe(customerOrders.Count, label);
        v.CustomerLifetimeValue.ShouldBe(customerOrders.Sum(o => o.Total), label);
        v.Display.ShouldBe(useEmail ? customer.Email : customer.Name, label);

        v.CityId.ShouldBe(city.Id, label);
        v.CityName.ShouldBe(city.Name, label);
        v.ProvinceId.ShouldBe(province.Id, label);
        v.ProvinceName.ShouldBe(province.Name, label);
        v.CountryId.ShouldBe(country.Id, label);
        v.CountryName.ShouldBe(country.Name, label);
        v.CountryNameDirect.ShouldBe(country.Name, label);

        v.ManagerId.ShouldBe(customer.ManagerId, label);
        v.ManagerName.ShouldBe(manager?.Name, label);

        var summary = v.Summary.ShouldNotBeNull(label);
        summary.Id.ShouldBe(customer.Id, label);
        summary.Name.ShouldBe(customer.Name, label);
        summary.Email.ShouldBe(customer.Email, label);
        summary.CityId.ShouldBe(city.Id, label);
        summary.CityName.ShouldBe(city.Name, $"{label}: second-round mapping of the returned object");

        var recent = customerOrders.OrderByDescending(o => o.OrderDate).Take(3).ToList();
        var actualRecent = v.RecentOrders.ShouldNotBeNull(label);
        actualRecent.Select(r => r.Id).ShouldBe(recent.Select(r => r.Id), label);
        actualRecent.Select(r => r.Status).ShouldBe(recent.Select(r => r.Status), label);
        actualRecent.Select(r => r.Total).ShouldBe(recent.Select(r => r.Total), label);

        v.Items.Count.ShouldBe(order.Items.Count, label);
        foreach (var item in v.Items)
        {
            var source = order.Items.Single(i => i.Id == item.Id);
            var product = d.Products.Single(p => p.Id == source.ProductId);
            var category = d.Categories.Single(c => c.Id == product.CategoryId);
            var parent = category.ParentId is { } pid ? d.Categories.Single(c => c.Id == pid) : null;
            var ratings = d.Reviews.Where(r => r.ProductId == product.Id).Select(r => r.Rating).ToList();
            var itemLabel = $"{label} item {item.Id}";

            item.ProductName.ShouldBe(product.Name, itemLabel);
            item.ProductPrice.ShouldBe(product.Price, itemLabel);
            item.CategoryId.ShouldBe(category.Id, itemLabel);
            item.CategoryName.ShouldBe(category.Name, itemLabel);
            item.CategoryParentName.ShouldBe(parent?.Name, itemLabel);
            item.ReviewCount.ShouldBe(ratings.Count, itemLabel);
            Near(item.ProductAvgRating, ratings.Count == 0 ? null : ratings.Average(), itemLabel);
            var tags = item.Tags.ShouldNotBeNull(itemLabel);
            tags.Select(t => t.Id).ShouldBe(product.Tags.OrderBy(t => t.Name, StringComparer.Ordinal).Select(t => t.Id),
                itemLabel);
            tags.Select(t => t.Name).ShouldBe(
                product.Tags.OrderBy(t => t.Name, StringComparer.Ordinal).Select(t => t.Name), itemLabel);
        }
    }

    private static void Near(double? actual, double? expected, string label)
    {
        if (expected is null) actual.ShouldBeNull(label);
        else actual.ShouldNotBeNull(label).ShouldBe(expected.Value, 1e-9, label);
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public async Task Every_field_of_every_order_matches_the_oracle(int seed, int orderCount)
    {
        using var fx = ComplexFixture.Create(seed, orderCount);
        var views = await fx.LoadOrderViews();
        views.Count.ShouldBe(orderCount);

        await fx.InScope(async (_, mapper) =>
        {
            await mapper.MapDataAsync(views);
            return 0;
        });

        foreach (var v in views) AssertOrder(v, fx.Data, useEmail: false);
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public async Task Conditional_expression_follows_the_flag_of_each_call(int seed, int orderCount)
    {
        using var fx = ComplexFixture.Create(seed, orderCount);

        fx.Mode.UseEmail = true;
        var first = await fx.LoadOrderViews();
        await fx.InScope(async (_, m) => { await m.MapDataAsync(first); return 0; });
        fx.Mode.UseEmail = false;
        var second = await fx.LoadOrderViews();
        await fx.InScope(async (_, m) => { await m.MapDataAsync(second); return 0; });

        foreach (var v in first) AssertOrder(v, fx.Data, useEmail: true);
        foreach (var v in second) AssertOrder(v, fx.Data, useEmail: false);
    }

    [Fact]
    public async Task Mapping_a_single_order_gives_the_same_result_as_mapping_the_whole_list()
    {
        using var fx = ComplexFixture.Create(7, 60);
        var all = await fx.LoadOrderViews();
        var one = (await fx.LoadOrderViews()).Skip(17).First();

        await fx.InScope(async (_, m) => { await m.MapDataAsync(all); return 0; });
        await fx.InScope(async (_, m) => { await m.MapDataAsync(one); return 0; });

        var expected = all.Single(v => v.Id == one.Id);
        System.Text.Json.JsonSerializer.Serialize(one).ShouldBe(System.Text.Json.JsonSerializer.Serialize(expected));
    }

    [Fact]
    public async Task Mapping_twice_changes_nothing()
    {
        using var fx = ComplexFixture.Create(8, 40);
        var views = await fx.LoadOrderViews();

        await fx.InScope(async (_, m) => { await m.MapDataAsync(views); return 0; });
        var once = System.Text.Json.JsonSerializer.Serialize(views);
        await fx.InScope(async (_, m) => { await m.MapDataAsync(views); return 0; });

        System.Text.Json.JsonSerializer.Serialize(views).ShouldBe(once);
    }

    [Fact]
    public async Task Parallel_mappings_in_separate_scopes_do_not_interfere()
    {
        using var fx = ComplexFixture.Create(9, 80);

        var tasks = Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
        {
            var views = await fx.LoadOrderViews();
            await fx.InScope(async (_, m) => { await m.MapDataAsync(views); return 0; });
            return views;
        })).ToList();

        foreach (var views in await Task.WhenAll(tasks))
        foreach (var v in views)
            AssertOrder(v, fx.Data, useEmail: false);
    }
}
