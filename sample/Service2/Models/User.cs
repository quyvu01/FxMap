using OfX.Fluent;
using Shared.Attributes;

namespace Service2.Models;

public sealed class User
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string ProvinceId { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class UserConfig : AbstractOfXConfig<User>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<UserOfAttribute>();
        ExposedName(x => x.Email, "UserEmail");
    }
}