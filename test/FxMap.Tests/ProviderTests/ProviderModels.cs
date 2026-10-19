using FxMap.Abstractions;
using FxMap.Fluent;

namespace FxMap.Tests.ProviderTests;

public class ProviderAddress
{
    public string City { get; set; }
    public string Country { get; set; }
}

public class ProviderOrder
{
    public int Number { get; set; }
    public string Status { get; set; }
    public decimal Total { get; set; }
}

/// <summary>String id, a nested object and a collection.</summary>
public class ProviderCustomer
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Nick { get; set; }
    public string Tags { get; set; }
    public int Score { get; set; }
    public DateTime JoinedAt { get; set; }
    public ProviderAddress Address { get; set; }
    public List<ProviderOrder> Orders { get; set; } = [];
}

/// <summary>Guid id.</summary>
public class ProviderDevice
{
    public Guid Id { get; set; }
    public string Label { get; set; }
}

/// <summary>int id.</summary>
public class ProviderTicket
{
    public int Id { get; set; }
    public string Title { get; set; }
}

/// <summary>Computed id: Mrn + Code.</summary>
public class ProviderLab
{
    public int Id { get; set; }
    public string Mrn { get; set; }
    public string Code { get; set; }
    public decimal Value { get; set; }
}

public sealed class ProviderCustomerKey : IDistributedKey;

public sealed class ProviderDeviceKey : IDistributedKey;

public sealed class ProviderTicketKey : IDistributedKey;

public sealed class ProviderLabKey : IDistributedKey;

public sealed class ProviderCustomerConfig : EntityConfigureOf<ProviderCustomer>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<ProviderCustomerKey>();
    }
}

public sealed class ProviderDeviceConfig : EntityConfigureOf<ProviderDevice>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Label);
        UseDistributedKey<ProviderDeviceKey>();
    }
}

public sealed class ProviderTicketConfig : EntityConfigureOf<ProviderTicket>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Title);
        UseDistributedKey<ProviderTicketKey>();
    }
}

public sealed class ProviderLabConfig : EntityConfigureOf<ProviderLab>
{
    protected override void Configure()
    {
        Id(x => x.Mrn + x.Code);
        DefaultProperty(x => x.Value);
        UseDistributedKey<ProviderLabKey>();
    }
}

/// <summary>A view filled from the four entities, to run the mapper end to end.</summary>
public class ProviderMappingView
{
    public string CustomerId { get; set; }
    public string Name { get; set; }
    public string DeviceId { get; set; }
    public string Label { get; set; }
    public string TicketId { get; set; }
    public string Title { get; set; }
}

/// <summary>A view with a valid expression and one that the owner cannot build.</summary>
public class ProviderBrokenExpressionView
{
    public string CustomerId { get; set; }
    public string Name { get; set; }
    public string Broken { get; set; } = "keep";
}

public sealed class ProviderBrokenExpressionViewProfile : ProfileOf<ProviderBrokenExpressionView>
{
    protected override void Configure() =>
        UseDistributedKey<ProviderCustomerKey>().Of(x => x.CustomerId)
            .For(x => x.Name)
            .For(x => x.Broken, "Name:bogus");
}

/// <summary>A view whose key is computed from two properties.</summary>
public class ProviderLabView
{
    public string Mrn { get; set; }
    public string Code { get; set; }
    public decimal? Value { get; set; }
}

public sealed class ProviderLabViewProfile : ProfileOf<ProviderLabView>
{
    protected override void Configure() =>
        UseDistributedKey<ProviderLabKey>().Of(x => x.Mrn + x.Code).For(x => x.Value);
}

public sealed class ProviderMappingViewProfile : ProfileOf<ProviderMappingView>
{
    protected override void Configure()
    {
        UseDistributedKey<ProviderCustomerKey>().Of(x => x.CustomerId).For(x => x.Name);
        UseDistributedKey<ProviderDeviceKey>().Of(x => x.DeviceId).For(x => x.Label);
        UseDistributedKey<ProviderTicketKey>().Of(x => x.TicketId).For(x => x.Title);
    }
}

/// <summary>The rows every provider is seeded with.</summary>
public static class ProviderData
{
    public static readonly Guid DeviceId = Guid.Parse("0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d");

    public static List<ProviderCustomer> Customers() =>
    [
        new()
        {
            Id = "c1", Name = "Alice", Email = "alice@example.com", Tags = "a,b,c", Score = 85,
            JoinedAt = new DateTime(2024, 12, 25, 14, 30, 45, DateTimeKind.Utc),
            Address = new ProviderAddress { City = "Hanoi", Country = "VN" },
            Orders =
            [
                new ProviderOrder { Number = 1, Status = "Done", Total = 1500.00m },
                new ProviderOrder { Number = 2, Status = "Done", Total = 350.00m },
                new ProviderOrder { Number = 3, Status = "Pending", Total = 2500.00m }
            ]
        },
        new()
        {
            Id = "c2", Name = "Bob", Email = "bob@example.com", Nick = "Bobby", Tags = "x", Score = 95,
            JoinedAt = new DateTime(2023, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Address = new ProviderAddress { City = "Saigon", Country = "VN" },
            Orders = [new ProviderOrder { Number = 1, Status = "Done", Total = 100.00m }]
        },
        // No address, no order
        new() { Id = "c3", Name = "Carol", Email = "carol@example.com", JoinedAt = new DateTime(2022, 6, 7, 8, 9, 10, DateTimeKind.Utc) }
    ];

    public static ProviderDevice Device() => new() { Id = DeviceId, Label = "scanner" };

    public static ProviderTicket Ticket() => new() { Id = 7, Title = "seven" };

    public static List<ProviderLab> Labs() =>
    [
        new() { Id = 1, Mrn = "MRN0001", Code = "GLU", Value = 5.40m },
        new() { Id = 2, Mrn = "MRN0001", Code = "HGB", Value = 13.80m },
        new() { Id = 3, Mrn = "MRN0002", Code = "GLU", Value = 7.10m }
    ];
}
