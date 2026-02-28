using OfX.Fluent;
using OfX.Tests.TestData.Attributes;

namespace OfX.Tests.TestData.Dtos;

/// <summary>
/// Test DTO demonstrating basic OfX attribute usage
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
        UseDistributedKey<UserOfAttribute>()
            .Of(x => x.UserId)
            .For(x => x.UserName)
            .For(x => x.UserEmail, "Email")
            .For(x => x.ProvinceId, "ProvinceId");

        UseDistributedKey<ProvinceOfAttribute>()
            .Of(x => x.ProvinceId)
            .For(x => x.ProvinceName)
            .For(x => x.CountryName, "Country.Name");
    }
}
