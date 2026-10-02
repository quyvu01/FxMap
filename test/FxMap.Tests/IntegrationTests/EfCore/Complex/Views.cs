using FxMap.Fluent;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Tests.IntegrationTests.EfCore.Complex;

/// <summary>Switch read by the conditional expression of <see cref="OrderView"/>.</summary>
public sealed class DisplayMode
{
    public bool UseEmail { get; set; }
}

#region OrderView: chains, aggregates, projections, conditional, nested objects, collections

public class TagView
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public class OrderBrief
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
}

/// <summary>Whole object returned by the remote, which then needs a second round of mapping (CityName).</summary>
public class CustomerSummaryView
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string CityId { get; set; } = "";
    public string CityName { get; set; }
}

public class OrderItemView
{
    public Guid Id { get; set; }
    public long ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public string ProductName { get; set; }
    public decimal ProductPrice { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; }
    public string CategoryParentName { get; set; }
    public double? ProductAvgRating { get; set; }
    public int ReviewCount { get; set; }
    public List<TagView> Tags { get; set; }
}

public class OrderView
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public DateTime OrderDate { get; set; }

    public string CustomerName { get; set; }
    public string CustomerEmail { get; set; }
    public Tier CustomerTier { get; set; }
    public bool CustomerIsVip { get; set; }
    public DateTime CustomerCreatedAt { get; set; }
    public int CustomerOrderCount { get; set; }
    public decimal CustomerLifetimeValue { get; set; }
    public string Display { get; set; }

    public string CityId { get; set; }
    public string CityName { get; set; }
    public Guid ProvinceId { get; set; }
    public string ProvinceName { get; set; }
    public int CountryId { get; set; }
    public string CountryName { get; set; }
    public string CountryNameDirect { get; set; }

    public Guid? ManagerId { get; set; }
    public string ManagerName { get; set; }

    public CustomerSummaryView Summary { get; set; }
    public List<OrderBrief> RecentOrders { get; set; }
    public List<OrderItemView> Items { get; set; } = [];
}

internal sealed class OrderViewProfile : ProfileOf<OrderView>
{
    protected override void Configure()
    {
        UseDistributedKey<KCustomer>().Of(x => x.CustomerId)
            .For(x => x.CustomerName, "Name")
            .For(x => x.CustomerEmail, "Email")
            .For(x => x.CustomerTier, "Tier")
            .For(x => x.CustomerIsVip, "IsVip")
            .For(x => x.CustomerCreatedAt, "CreatedAt")
            .For(x => x.CustomerOrderCount, "Orders:count")
            .For(x => x.CustomerLifetimeValue, "Orders:sum(Total)")
            .For(x => x.CityId, "CityId")
            .For(x => x.ManagerId, "ManagerId")
            .For(x => x.Summary, "{Id, Name, Email, CityId}")
            .For(x => x.RecentOrders, "Orders[0 3 desc OrderDate].{Id, Status, Total}")
            .For(x => x.Display, c => c
                .If(sp => sp.GetRequiredService<DisplayMode>().UseEmail)
                .Expression("Email")
                .Else("Name"));

        UseDistributedKey<KCity>().Of(x => x.CityId)
            .For(x => x.CityName, "Name")
            .For(x => x.ProvinceId, "ProvinceId")
            .For(x => x.CountryNameDirect, "Province.Country.Name");

        UseDistributedKey<KProvince>().Of(x => x.ProvinceId)
            .For(x => x.ProvinceName, "Name")
            .For(x => x.CountryId, "CountryId");

        UseDistributedKey<KCountry>().Of(x => x.CountryId)
            .For(x => x.CountryName, "Name");

        // Same key as the first group but one level deeper (ManagerId is itself a mapped property).
        UseDistributedKey<KCustomer>().Of(x => x.ManagerId)
            .For(x => x.ManagerName, "Name");
    }
}

internal sealed class CustomerSummaryViewProfile : ProfileOf<CustomerSummaryView>
{
    protected override void Configure() =>
        UseDistributedKey<KCity>().Of(x => x.CityId).For(x => x.CityName, "Name");
}

internal sealed class OrderItemViewProfile : ProfileOf<OrderItemView>
{
    protected override void Configure()
    {
        UseDistributedKey<KProduct>().Of(x => x.ProductId)
            .For(x => x.ProductName, "Name")
            .For(x => x.ProductPrice, "Cost")
            .For(x => x.CategoryId, "CategoryId")
            .For(x => x.ProductAvgRating, "Reviews:avg(Rating)")
            .For(x => x.ReviewCount, "Reviews:count")
            .For(x => x.Tags, "Tags[asc Name].{Id, Name}");

        UseDistributedKey<KCategory>().Of(x => x.CategoryId)
            .For(x => x.CategoryName, "Name")
            .For(x => x.CategoryParentName, "Parent.Name");
    }
}

#endregion

#region Statistics of one customer (aggregations and filters)

public class CustomerStatsView
{
    public Guid CustomerId { get; set; }
    public int Total { get; set; }
    public int Done { get; set; }
    public int DoneAndBig { get; set; }
    public int NotCancelled { get; set; }
    public decimal Sum { get; set; }
    public decimal? Max { get; set; }
    public decimal? Min { get; set; }
    public decimal? Avg { get; set; }
    public bool AnyPending { get; set; }
    public bool AllPositive { get; set; }
    public OrderBrief Latest { get; set; }
    public List<OrderBrief> DoneOrders { get; set; }
}

internal sealed class CustomerStatsViewProfile : ProfileOf<CustomerStatsView>
{
    protected override void Configure() =>
        UseDistributedKey<KCustomer>().Of(x => x.CustomerId)
            .For(x => x.Total, "Orders:count")
            .For(x => x.Done, "Orders(Status = 'Done'):count")
            .For(x => x.DoneAndBig, "Orders(Status = 'Done', Total > 100):count")
            .For(x => x.NotCancelled, "Orders(Status != 'Cancelled'):count")
            .For(x => x.Sum, "Orders:sum(Total)")
            .For(x => x.Max, "Orders:max(Total)")
            .For(x => x.Min, "Orders:min(Total)")
            .For(x => x.Avg, "Orders:avg(Total)")
            .For(x => x.AnyPending, "Orders:any(Status = 'Pending')")
            .For(x => x.AllPositive, "Orders:all(Total > 0)")
            .For(x => x.Latest, "Orders[0 desc OrderDate].{Id, Status, Total}")
            .For(x => x.DoneOrders, "Orders(Status = 'Done')[asc OrderDate].{Id, Status, Total}");
}

#endregion

#region Country report (navigation collections)

public class ProvinceBrief
{
    public string Name { get; set; } = "";
    public long Area { get; set; }
}

public class CountryReportView
{
    public int CountryId { get; set; }
    public string Name { get; set; }
    public int ProvinceCount { get; set; }
    public long TotalArea { get; set; }
    public long? MaxArea { get; set; }
    public List<ProvinceBrief> BigProvinces { get; set; }
}

internal sealed class CountryReportViewProfile : ProfileOf<CountryReportView>
{
    protected override void Configure() =>
        UseDistributedKey<KCountry>().Of(x => x.CountryId)
            .For(x => x.Name, "Name")
            .For(x => x.ProvinceCount, "Provinces:count")
            .For(x => x.TotalArea, "Provinces:sum(Area)")
            .For(x => x.MaxArea, "Provinces:max(Area)")
            .For(x => x.BigProvinces, "Provinces(Area > 400000)[asc Name].{Name, Area}");
}

#endregion

#region Category tree: the mapped value is a list of objects that are mapped again, level after level

public class CategoryTreeView
{
    public int CategoryId { get; set; }
    public string Name { get; set; }
    public string ParentName { get; set; }
    public List<CategoryTreeView> Children { get; set; }
}

internal sealed class CategoryTreeViewProfile : ProfileOf<CategoryTreeView>
{
    protected override void Configure() =>
        UseDistributedKey<KCategory>().Of(x => x.CategoryId)
            .For(x => x.Name, "Name")
            .For(x => x.ParentName, "Parent.Name")
            .For(x => x.Children, "Children[asc Name].{Id as CategoryId}");
}

#endregion

#region Order key (a DTO that only knows the order id)

public class ShipmentView
{
    public Guid OrderId { get; set; }
    public string OrderStatus { get; set; }
    public int ItemCount { get; set; }
    public int TotalQuantity { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; }
}

internal sealed class ShipmentViewProfile : ProfileOf<ShipmentView>
{
    protected override void Configure()
    {
        UseDistributedKey<KOrder>().Of(x => x.OrderId)
            .For(x => x.OrderStatus, "Status")
            .For(x => x.ItemCount, "Items:count")
            .For(x => x.TotalQuantity, "Items:sum(Quantity)")
            .For(x => x.CustomerId, "CustomerId");
        UseDistributedKey<KCustomer>().Of(x => x.CustomerId).For(x => x.CustomerName, "Name");
    }
}

#endregion

#region String key and exposed names

public class TagCardView
{
    public string TagId { get; set; } = "";
    public string TagName { get; set; }
    public int ProductCount { get; set; }
}

internal sealed class TagCardViewProfile : ProfileOf<TagCardView>
{
    protected override void Configure() =>
        UseDistributedKey(TagConfig.Key).Of(x => x.TagId)
            .For(x => x.TagName)
            .For(x => x.ProductCount, "Products:count");
}

public class ProductCardView
{
    public long ProductId { get; set; }
    public decimal Cost { get; set; }
    public string Name { get; set; }
}

internal sealed class ProductCardViewProfile : ProfileOf<ProductCardView>
{
    protected override void Configure() =>
        UseDistributedKey<KProduct>().Of(x => x.ProductId)
            .For(x => x.Cost, "Cost")
            .For(x => x.Name);
}

public class OrderDateView
{
    public Guid CustomerId { get; set; }
    public DateTime? FirstOrder { get; set; }
    public DateTime? LastOrder { get; set; }
    public string MaxStatus { get; set; }
    public Guid? MaxId { get; set; }
}

internal sealed class OrderDateViewProfile : ProfileOf<OrderDateView>
{
    protected override void Configure() =>
        UseDistributedKey<KCustomer>().Of(x => x.CustomerId)
            .For(x => x.FirstOrder, "Orders:min(OrderDate)")
            .For(x => x.LastOrder, "Orders:max(OrderDate)")
            .For(x => x.MaxStatus, "Orders:max(Status)")
            .For(x => x.MaxId, "Orders:max(Id)");
}

#endregion

#region Selectors whose text form differs from the canonical form of the entity id

/// <summary>Ids kept as text by another service: any format the id type can parse (upper case, braces, no hyphens...).</summary>
public class LooseIdView
{
    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; }
    public string CountryId { get; set; } = "";
    public string CountryName { get; set; }
    public string ProductId { get; set; } = "";
    public string ProductName { get; set; }
}

internal sealed class LooseIdViewProfile : ProfileOf<LooseIdView>
{
    protected override void Configure()
    {
        UseDistributedKey<KCustomer>().Of(x => x.CustomerId).For(x => x.CustomerName, "Name");
        UseDistributedKey<KCountry>().Of(x => x.CountryId).For(x => x.CountryName, "Name");
        UseDistributedKey<KProduct>().Of(x => x.ProductId).For(x => x.ProductName, "Name");
    }
}

#endregion
