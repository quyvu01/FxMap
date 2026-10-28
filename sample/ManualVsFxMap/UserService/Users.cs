using FxMap.Fluent;
using Microsoft.EntityFrameworkCore;

namespace UserService;

public sealed class User
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public sealed class UserDb(DbContextOptions<UserDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    /// <summary>12 users, u-01 to u-12.</summary>
    public static IEnumerable<User> Seed() => Enumerable.Range(1, 12).Select(i => new User
    {
        Id = $"u-{i:00}",
        Name = $"User {i:00}",
        Email = $"user{i:00}@shop.example"
    });
}

/// <summary>What this service exposes to FxMap: users under the string key "UserOf", answered from EF Core.</summary>
public sealed class UserConfig : EntityConfigureOf<User>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey("UserOf");
    }
}
