using FxMap.Fluent;

namespace FxMap.Benchmark.Projection;

/// <summary>Same DTO is the output of both AutoMapper ProjectTo and FxMap enrichment.</summary>
public class OrderDto
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public DateTime OrderDate { get; set; }

    public string CustomerId { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string ProvinceId { get; set; } = "";
    public string ProvinceName { get; set; } = "";
    public string CountryName { get; set; } = "";

    public List<OrderItemDto> Items { get; set; } = [];
}

public class OrderItemDto
{
    public string Id { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }

    public string ProductId { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public string CategoryName { get; set; } = "";
}

public class OrderDtoProfile : ProfileOf<OrderDto>
{
    protected override void Configure()
    {
        UseDistributedKey<CustomerKey>()
            .Of(x => x.CustomerId)
            .For(x => x.CustomerName, "Name")
            .For(x => x.CustomerEmail, "Email")
            .For(x => x.ProvinceId, "ProvinceId");

        UseDistributedKey<ProvinceKey>()
            .Of(x => x.ProvinceId)
            .For(x => x.ProvinceName, "Name")
            .For(x => x.CountryName, "Country.Name");
    }
}

public class OrderItemDtoProfile : ProfileOf<OrderItemDto>
{
    protected override void Configure()
    {
        UseDistributedKey<ProductKey>()
            .Of(x => x.ProductId)
            .For(x => x.ProductName, "Name")
            .For(x => x.CategoryId, "CategoryId");

        UseDistributedKey<CategoryKey>()
            .Of(x => x.CategoryId)
            .For(x => x.CategoryName, "Name");
    }
}
