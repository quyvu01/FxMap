using FxMap.Exceptions;
using Shouldly;
using Xunit;

namespace FxMap.Tests.IntegrationTests.EfCore.Complex;

/// <summary>Aggregations, filters, navigation collections, recursive trees, string keys and error handling on real EF queries.</summary>
[Collection("EfCore Sequential")]
public class ComplexExpressionMappingTests
{
    private static async Task<T> Map<T>(ComplexFixture fx, T view) where T : class
    {
        await fx.InScope(async (_, m) =>
        {
            await m.MapDataAsync(view);
            return 0;
        });
        return view;
    }

    public static TheoryData<int, int> Sizes() => new() { { 11, 6 }, { 12, 45 }, { 13, 150 } };

    #region Aggregations over customers, including customers without any order

    [Theory]
    [MemberData(nameof(Sizes))]
    public async Task Customer_statistics_match_the_oracle_for_every_customer(int seed, int orderCount)
    {
        using var fx = ComplexFixture.Create(seed, orderCount);
        var d = fx.Data;
        var views = d.Customers.Select(c => new CustomerStatsView { CustomerId = c.Id }).ToList();
        views.Count(v => d.Orders.All(o => o.CustomerId != v.CustomerId)).ShouldBeGreaterThan(0,
            "the data set must contain customers without orders to exercise the empty paths");

        await Map(fx, views);

        foreach (var v in views)
        {
            var orders = d.Orders.Where(o => o.CustomerId == v.CustomerId).ToList();
            var label = $"customer {v.CustomerId} ({orders.Count} orders)";
            v.Total.ShouldBe(orders.Count, label);
            v.Done.ShouldBe(orders.Count(o => o.Status == "Done"), label);
            v.DoneAndBig.ShouldBe(orders.Count(o => o.Status == "Done" && o.Total > 100), label);
            v.NotCancelled.ShouldBe(orders.Count(o => o.Status != "Cancelled"), label);
            v.Sum.ShouldBe(orders.Sum(o => o.Total), label);
            v.Max.ShouldBe(orders.Count == 0 ? null : orders.Max(o => o.Total), label);
            v.Min.ShouldBe(orders.Count == 0 ? null : orders.Min(o => o.Total), label);
            v.Avg.ShouldBe(orders.Count == 0 ? null : orders.Average(o => o.Total), label);
            v.AnyPending.ShouldBe(orders.Any(o => o.Status == "Pending"), label);
            v.AllPositive.ShouldBe(orders.All(o => o.Total > 0), label);

            var done = orders.Where(o => o.Status == "Done").OrderBy(o => o.OrderDate).ToList();
            v.DoneOrders.ShouldNotBeNull(label);
            v.DoneOrders!.Select(o => o.Id).ShouldBe(done.Select(o => o.Id), label);

            if (orders.Count == 0) continue;
            var latest = orders.OrderByDescending(o => o.OrderDate).First();
            v.Latest.ShouldNotBeNull(label);
            v.Latest!.Id.ShouldBe(latest.Id, label);
            v.Latest.Total.ShouldBe(latest.Total, label);
        }
    }

    [Fact]
    public async Task Min_and_max_work_on_dates_strings_and_guids_and_are_null_for_an_empty_collection()
    {
        using var fx = ComplexFixture.Create(21, 50);
        var d = fx.Data;
        var views = d.Customers.Select(c => new OrderDateView { CustomerId = c.Id }).ToList();

        await Map(fx, views);

        foreach (var v in views)
        {
            var orders = d.Orders.Where(o => o.CustomerId == v.CustomerId).ToList();
            if (orders.Count == 0)
            {
                v.FirstOrder.ShouldBeNull();
                v.LastOrder.ShouldBeNull();
                v.MaxStatus.ShouldBeNull();
                v.MaxId.ShouldBeNull();
                continue;
            }

            v.FirstOrder.ShouldBe(orders.Min(o => o.OrderDate));
            v.LastOrder.ShouldBe(orders.Max(o => o.OrderDate));
            v.MaxStatus.ShouldBe(orders.Max(o => o.Status), "string comparison of the provider");
            v.MaxId.ShouldBe(orders.Max(o => o.Id));
        }
    }

    [Fact]
    public async Task One_customer_without_orders_does_not_break_the_values_of_the_other_customers()
    {
        using var fx = ComplexFixture.Create(22, 30);
        var d = fx.Data;
        var withOrders = d.Customers.First(c => d.Orders.Any(o => o.CustomerId == c.Id));
        var without = d.Customers.First(c => d.Orders.All(o => o.CustomerId != c.Id));
        var views = new List<CustomerStatsView>
        {
            new() { CustomerId = withOrders.Id }, new() { CustomerId = without.Id }
        };

        await Map(fx, views);

        views[0].Max.ShouldBe(d.Orders.Where(o => o.CustomerId == withOrders.Id).Max(o => o.Total));
        views[1].Max.ShouldBeNull();
        views[1].Total.ShouldBe(0);
    }

    [Fact]
    public async Task Single_item_projection_of_an_empty_collection_throws_when_configured_to_throw()
    {
        // Characterization: `Orders[0 desc OrderDate].{Id, Status, Total}` on a customer without orders yields an
        // object whose members are all null (this is asserted by ExpressionIntegrationTests as well), which cannot be
        // deserialized into OrderBrief's non-nullable members.
        using var fx = ComplexFixture.Create(23, 4, cfg => cfg.ThrowIfException());
        var d = fx.Data;
        var without = d.Customers.First(c => d.Orders.All(o => o.CustomerId != c.Id));

        await Should.ThrowAsync<Exception>(() => Map(fx, new CustomerStatsView { CustomerId = without.Id }));
    }

    [Fact]
    public async Task Single_item_projection_of_an_empty_collection_leaves_the_property_null_by_default()
    {
        using var fx = ComplexFixture.Create(23, 4);
        var d = fx.Data;
        var without = d.Customers.First(c => d.Orders.All(o => o.CustomerId != c.Id));
        var view = new CustomerStatsView { CustomerId = without.Id };

        await Map(fx, view);

        view.Latest.ShouldBeNull();
        view.Total.ShouldBe(0);
        view.AllPositive.ShouldBeTrue("vacuous truth");
        view.AnyPending.ShouldBeFalse();
    }

    #endregion

    #region Navigation collections, aggregates, filters on projections

    [Fact]
    public async Task Country_report_matches_the_oracle()
    {
        using var fx = ComplexFixture.Create(31, 10);
        var d = fx.Data;
        var views = d.Countries.Select(c => new CountryReportView { CountryId = c.Id }).ToList();

        await Map(fx, views);

        foreach (var v in views)
        {
            var provinces = d.Provinces.Where(p => p.CountryId == v.CountryId).ToList();
            v.Name.ShouldBe(d.Countries.Single(c => c.Id == v.CountryId).Name);
            v.ProvinceCount.ShouldBe(provinces.Count);
            v.TotalArea.ShouldBe(provinces.Sum(p => p.Area));
            v.MaxArea.ShouldBe(provinces.Count == 0 ? null : provinces.Max(p => p.Area));
            var big = provinces.Where(p => p.Area > 400_000).OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
            v.BigProvinces.ShouldNotBeNull();
            v.BigProvinces!.Select(p => p.Name).ShouldBe(big.Select(p => p.Name));
            v.BigProvinces!.Select(p => p.Area).ShouldBe(big.Select(p => p.Area));
        }
    }

    [Fact]
    public async Task Shipment_resolves_order_aggregates_and_the_customer_behind_them()
    {
        using var fx = ComplexFixture.Create(32, 60);
        var d = fx.Data;
        var views = d.Orders.Select(o => new ShipmentView { OrderId = o.Id }).ToList();

        await Map(fx, views);

        foreach (var v in views)
        {
            var order = d.Orders.Single(o => o.Id == v.OrderId);
            v.OrderStatus.ShouldBe(order.Status);
            v.ItemCount.ShouldBe(order.Items.Count);
            v.TotalQuantity.ShouldBe(order.Items.Sum(i => i.Quantity));
            v.CustomerId.ShouldBe(order.CustomerId);
            v.CustomerName.ShouldBe(d.Customers.Single(c => c.Id == order.CustomerId).Name);
        }
    }

    #endregion

    #region Recursive tree: mapped list of objects that are mapped again, level after level

    private static void AssertTree(CategoryTreeView v, DomainData d)
    {
        var category = d.Categories.Single(c => c.Id == v.CategoryId);
        v.Name.ShouldBe(category.Name);
        v.ParentName.ShouldBe(category.ParentId is { } p ? d.Categories.Single(c => c.Id == p).Name : null);
        var children = d.Categories.Where(c => c.ParentId == category.Id)
            .OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        v.Children.ShouldNotBeNull($"category {category.Id} must get its children list (possibly empty)");
        v.Children!.Select(c => c.CategoryId).ShouldBe(children.Select(c => c.Id));
        foreach (var child in v.Children!) AssertTree(child, d);
    }

    [Fact]
    public async Task Category_tree_is_expanded_to_every_depth()
    {
        using var fx = ComplexFixture.Create(41, 10);
        var d = fx.Data;
        var roots = d.Categories.Where(c => c.ParentId is null).Select(c => new CategoryTreeView { CategoryId = c.Id })
            .ToList();

        await Map(fx, roots);

        roots.Count.ShouldBe(3);
        foreach (var root in roots) AssertTree(root, d);
        roots.SelectMany(r => r.Children!).SelectMany(c => c.Children!).ShouldAllBe(l => l.Children!.Count == 0);
    }

    [Fact]
    public async Task Category_tree_needs_exactly_as_many_levels_as_it_is_deep()
    {
        // 3 levels of categories (root, child, leaf) -> 3 mapping levels. The leaves' empty child lists must not
        // require a fourth one.
        using var fx = ComplexFixture.Create(42, 10, cfg =>
        {
            cfg.SetMaxNestingDepth(3);
            cfg.ThrowIfException();
        });
        var d = fx.Data;
        var root = new CategoryTreeView { CategoryId = d.Categories.First(c => c.ParentId is null).Id };

        await Map(fx, root);

        AssertTree(root, d);
    }

    [Fact]
    public async Task Category_tree_deeper_than_the_configured_depth_throws_when_configured_to_throw()
    {
        using var fx = ComplexFixture.Create(43, 10, cfg =>
        {
            cfg.SetMaxNestingDepth(2);
            cfg.ThrowIfException();
        });
        var d = fx.Data;
        var root = new CategoryTreeView { CategoryId = d.Categories.First(c => c.ParentId is null).Id };

        await Should.ThrowAsync<DistributedMapException.MaxNestingDepthReached>(() => Map(fx, root));
    }

    #endregion

    #region String key, exposed name, unknown ids

    [Fact]
    public async Task String_distributed_key_maps_like_a_type_key()
    {
        using var fx = ComplexFixture.Create(51, 10);
        var d = fx.Data;
        var views = d.Tags.Select(t => new TagCardView { TagId = t.Id }).ToList();

        await Map(fx, views);

        foreach (var v in views)
        {
            v.TagName.ShouldBe(d.Tags.Single(t => t.Id == v.TagId).Name);
            v.ProductCount.ShouldBe(d.Products.Count(p => p.Tags.Any(t => t.Id == v.TagId)));
        }
    }

    [Fact]
    public async Task Exposed_name_replaces_the_property_name_in_expressions()
    {
        using var fx = ComplexFixture.Create(52, 10);
        var d = fx.Data;
        var views = d.Products.Take(10).Select(p => new ProductCardView { ProductId = p.Id }).ToList();

        await Map(fx, views);

        foreach (var v in views)
        {
            var product = d.Products.Single(p => p.Id == v.ProductId);
            v.Cost.ShouldBe(product.Price);
            v.Name.ShouldBe(product.Name, "no expression -> default property");
        }
    }

    [Fact]
    public async Task Ids_that_do_not_exist_leave_the_view_untouched_and_do_not_affect_the_others()
    {
        using var fx = ComplexFixture.Create(53, 20);
        var d = fx.Data;
        var real = d.Customers[0];
        var views = new List<CustomerStatsView>
        {
            new() { CustomerId = Guid.NewGuid() },
            new() { CustomerId = real.Id },
            new() { CustomerId = Guid.Empty }
        };

        await Map(fx, views);

        views[0].Total.ShouldBe(0);
        views[0].Max.ShouldBeNull();
        views[1].Total.ShouldBe(d.Orders.Count(o => o.CustomerId == real.Id));
        views[2].Total.ShouldBe(0);
    }

    #endregion

    #region Id text formats

    [Fact]
    public async Task Selector_text_in_another_format_than_the_canonical_id_is_still_mapped()
    {
        using var fx = ComplexFixture.Create(61, 12);
        var d = fx.Data;
        var customer = d.Customers[0];
        var country = d.Countries[0];
        var product = d.Products[0];
        var formats = new (string Customer, string Country, string Product)[]
        {
            (customer.Id.ToString("D").ToUpperInvariant(), country.Id.ToString("D3"), product.Id.ToString("D12")),
            (customer.Id.ToString("N"), $" {country.Id} ", $"+{product.Id}"),
            (customer.Id.ToString("B"), country.Id.ToString(), product.Id.ToString()),
            (customer.Id.ToString("D"), $"00{country.Id}", $"{product.Id}")
        };

        foreach (var (customerId, countryId, productId) in formats)
        {
            var view = new LooseIdView { CustomerId = customerId, CountryId = countryId, ProductId = productId };

            await Map(fx, view);

            view.CustomerName.ShouldBe(customer.Name, $"customer id as '{customerId}'");
            view.CountryName.ShouldBe(country.Name, $"country id as '{countryId}'");
            view.ProductName.ShouldBe(product.Name, $"product id as '{productId}'");
        }
    }

    [Fact]
    public async Task Same_entity_referenced_with_different_id_formats_in_one_call_maps_every_view()
    {
        using var fx = ComplexFixture.Create(62, 12);
        var customer = fx.Data.Customers[1];
        var views = new[]
        {
            new LooseIdView { CustomerId = customer.Id.ToString("D") },
            new LooseIdView { CustomerId = customer.Id.ToString("D").ToUpperInvariant() },
            new LooseIdView { CustomerId = customer.Id.ToString("N") }
        };

        await Map(fx, views);

        views.ShouldAllBe(v => v.CustomerName == customer.Name);
    }

    [Fact]
    public async Task Selector_text_that_cannot_be_parsed_is_ignored()
    {
        using var fx = ComplexFixture.Create(63, 12);
        var customer = fx.Data.Customers[2];
        var views = new[]
        {
            new LooseIdView { CustomerId = "not-a-guid", CountryId = "abc", ProductId = "12x" },
            new LooseIdView { CustomerId = customer.Id.ToString() }
        };

        await Map(fx, views);

        views[0].CustomerName.ShouldBeNull();
        views[0].CountryName.ShouldBeNull();
        views[0].ProductName.ShouldBeNull();
        views[1].CustomerName.ShouldBe(customer.Name);
    }

    #endregion

    #region Request shape

    [Fact]
    public async Task The_same_expressions_in_a_different_order_give_the_same_values()
    {
        using var fx = ComplexFixture.Create(71, 20);
        var customer = fx.Data.Customers[0];
        var ids = new[] { customer.Id.ToString() };

        async Task<string[]> Ask(params string[] expressions)
        {
            var response = await fx.InScope((_, m) =>
                m.FetchDataAsync<KCustomer>(new FxMap.Models.DistributedMapRequest(ids, expressions)));
            var values = response.Items.Single().Values;
            return [..expressions.Select(e => values.Single(v => v.Expression == e).Value)];
        }

        var forward = await Ask("Name", "Email", "Orders:count");
        var reverse = await Ask("Orders:count", "Email", "Name");
        var subset = await Ask("Email");

        forward.ShouldBe([reverse[2], reverse[1], reverse[0]]);
        subset[0].ShouldBe(forward[1]);
    }

    [Fact]
    public async Task Large_data_set_is_mapped_completely()
    {
        using var fx = ComplexFixture.Create(72, 1500);
        var views = await fx.LoadOrderViews();
        views.Count.ShouldBe(1500);

        await Map(fx, views);

        views.ShouldAllBe(v => v.CustomerName != null && v.CountryName != null && v.Summary != null);
        views.SelectMany(v => v.Items).ShouldAllBe(i => i.ProductName != null && i.CategoryName != null);
        foreach (var v in views.Where((_, i) => i % 37 == 0)) AssertLarge(v, fx.Data);
    }

    private static void AssertLarge(OrderView v, DomainData d)
    {
        var order = d.Orders.Single(o => o.Id == v.Id);
        var customer = d.Customers.Single(c => c.Id == order.CustomerId);
        v.CustomerName.ShouldBe(customer.Name);
        v.CustomerOrderCount.ShouldBe(d.Orders.Count(o => o.CustomerId == customer.Id));
        v.Items.Select(i => i.ProductName).ShouldBe(order.Items.Select(i => d.Products.Single(p => p.Id == i.ProductId).Name));
    }

    #endregion
}
