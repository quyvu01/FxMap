using FxMap.Fluent;
using FxMap.Tests.TestData.DistributedKeys;

namespace FxMap.Tests.TestData.Dtos;

/// <summary>
/// Test DTO demonstrating basic FxMap attribute usage
/// </summary>
public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string UserEmail { get; set; } = string.Empty;

    public string ProvinceId { get; set; } = string.Empty;

    public string ProvinceName { get; set; } = string.Empty;

    public string CountryName { get; set; } = string.Empty;
}

public class UserResponseProfile : ProfileOf<UserResponse>
{
    protected override void Configure()
    {
        UseDistributedKey<UserDistributedKey>()
            .Of(x => x.UserId)
            .For(x => x.UserName)
            .For(x => x.UserEmail, "Email")
            .For(x => x.ProvinceId, "ProvinceId");

        UseDistributedKey<ProvinceDistributedKey>()
            .Of(x => x.ProvinceId)
            .For(x => x.ProvinceName)
            .For(x => x.CountryName, "Country.Name");
    }
}
