namespace FxMap.Tests.IntegrationTests.EfCore.Complex;

/// <summary>Deterministic data set. The lists double as the oracle: expectations are computed from them with plain LINQ.</summary>
public sealed class DomainData
{
    public List<DCountry> Countries { get; } = [];
    public List<DProvince> Provinces { get; } = [];
    public List<DCity> Cities { get; } = [];
    public List<DCustomer> Customers { get; } = [];
    public List<DCategory> Categories { get; } = [];
    public List<DTag> Tags { get; } = [];
    public List<DProduct> Products { get; } = [];
    public List<DReview> Reviews { get; } = [];
    public List<DOrder> Orders { get; } = [];

    public static readonly string[] Statuses = ["Done", "Pending", "Cancelled", "Shipped"];

    private static Guid NewGuid(Random rnd)
    {
        var bytes = new byte[16];
        rnd.NextBytes(bytes);
        return new Guid(bytes);
    }

    public static DomainData Generate(int seed, int orderCount)
    {
        var rnd = new Random(seed);
        var d = new DomainData();

        for (var i = 1; i <= 3; i++) d.Countries.Add(new DCountry { Id = i * 10, Name = $"Country {i}" });
        for (var i = 0; i < 6; i++)
            d.Provinces.Add(new DProvince
            {
                Id = NewGuid(rnd), Name = $"Province {i:00}", Area = rnd.Next(1, 10) * 100_000L,
                CountryId = d.Countries[i % 3].Id
            });
        for (var i = 0; i < 14; i++)
            d.Cities.Add(new DCity
            {
                Id = $"city-{i:00}", Name = $"City {i:00}", Population = rnd.Next(1_000, 2_000_000),
                ProvinceId = d.Provinces[i % 6].Id
            });

        // Category tree: 3 roots, 3 children each, 2 grandchildren each (30 nodes).
        var catId = 1;
        foreach (var r in Enumerable.Range(0, 3))
        {
            var root = new DCategory { Id = catId++, Name = $"Root {r}" };
            d.Categories.Add(root);
            foreach (var c in Enumerable.Range(0, 3))
            {
                var child = new DCategory { Id = catId++, Name = $"Child {r}.{c}", ParentId = root.Id };
                d.Categories.Add(child);
                foreach (var g in Enumerable.Range(0, 2))
                    d.Categories.Add(new DCategory { Id = catId++, Name = $"Leaf {r}.{c}.{g}", ParentId = child.Id });
            }
        }

        for (var i = 0; i < 10; i++) d.Tags.Add(new DTag { Id = $"tag-{i}", Name = $"Tag {(char)('A' + i)}" });
        var leafCategories = d.Categories.Where(c => d.Categories.All(o => o.ParentId != c.Id)).ToList();
        for (var i = 0; i < 40; i++)
        {
            var product = new DProduct
            {
                Id = 5_000_000_000L + i, Name = $"Product {i:00}", Price = rnd.Next(1, 500) + 0.5m,
                CategoryId = leafCategories[rnd.Next(leafCategories.Count)].Id
            };
            foreach (var tag in d.Tags.OrderBy(_ => rnd.Next()).Take(rnd.Next(0, 4))) product.Tags.Add(tag);
            d.Products.Add(product);
        }

        var customerCount = Math.Max(5, orderCount / 3);
        for (var i = 0; i < customerCount; i++)
            d.Customers.Add(new DCustomer
            {
                Id = NewGuid(rnd), Name = $"Customer {i:000}", Email = $"customer{i}@example.com",
                Tier = (Tier)rnd.Next(0, 3), IsVip = rnd.Next(4) == 0,
                CreatedAt = new DateTime(2024, 1, 1).AddDays(rnd.Next(0, 700)), CityId = d.Cities[rnd.Next(d.Cities.Count)].Id
            });
        // Managers: roughly half of the customers have one, always an earlier customer (no cycles).
        for (var i = 1; i < d.Customers.Count; i++)
            if (rnd.Next(2) == 0)
                d.Customers[i].ManagerId = d.Customers[rnd.Next(i)].Id;

        // Reviews on only some products: others must exercise the "no review" paths.
        var reviewId = 1;
        foreach (var product in d.Products.Where((_, i) => i % 4 != 0))
            for (var k = rnd.Next(0, 6); k > 0; k--)
                d.Reviews.Add(new DReview
                {
                    Id = reviewId++, ProductId = product.Id, Rating = rnd.Next(1, 6),
                    CustomerId = d.Customers[rnd.Next(d.Customers.Count)].Id
                });

        for (var i = 0; i < orderCount; i++)
        {
            var order = new DOrder
            {
                Id = NewGuid(rnd), CustomerId = d.Customers[rnd.Next(d.Customers.Count)].Id,
                Status = Statuses[rnd.Next(Statuses.Length)],
                OrderDate = new DateTime(2025, 1, 1).AddHours(i * 7 + rnd.Next(0, 5))
            };
            for (var k = rnd.Next(1, 6); k > 0; k--)
            {
                var product = d.Products[rnd.Next(d.Products.Count)];
                var item = new DOrderItem
                {
                    Id = NewGuid(rnd), OrderId = order.Id, ProductId = product.Id, Quantity = rnd.Next(1, 5),
                    UnitPrice = product.Price
                };
                order.Items.Add(item);
                order.Total += item.Quantity * item.UnitPrice;
            }

            d.Orders.Add(order);
        }

        return d;
    }
}
