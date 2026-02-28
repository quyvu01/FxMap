using FxMap.Fluent;
using Shared.Attributes;

namespace Service1.Contract.Responses;

public sealed class UserResponse
{
    public string Id { get; set; }
    public string UserEmail { get; set; }
    public string ProvinceId { get; set; }
    public ProvinceComplexResponse ProvinceResponse { get; set; }
}

public class UserResponseProfile : ProfileOf<UserResponse>
{
    protected override void Configure()
    {
        UseDistributedKey<ProvinceOfAttribute>()
            .Of(x => x.ProvinceId)
            .For(x => x.ProvinceResponse, "{Id, Name, Country.Name as CountryName, CountryId}");
    }
}