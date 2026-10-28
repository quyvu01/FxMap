using FxMap.Fluent;

namespace OrderService;

public sealed class OrderResponse
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
}

/// <summary>Where the user fields come from: the "UserOf" key, which the user service owns.</summary>
public sealed class OrderResponseProfile : ProfileOf<OrderResponse>
{
    protected override void Configure() =>
        UseDistributedKey("UserOf")
            .Of(x => x.UserId)
            .For(x => x.UserName)
            .For(x => x.UserEmail, "Email");
}

public sealed record UserDto(string Id, string Name, string Email);

public static class Orders
{
    /// <summary>
    /// 100 orders from 12 users (u-01 to u-12), plus order 100 whose user u-99 does not exist,
    /// to show what each approach does with a missing user.
    /// </summary>
    public static List<OrderResponse> Load() =>
        Enumerable.Range(1, 100)
            .Select(i => new OrderResponse { Id = i, UserId = i == 100 ? "u-99" : $"u-{(i - 1) % 12 + 1:00}" })
            .ToList();
}
