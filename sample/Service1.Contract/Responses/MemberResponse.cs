using FxMap.Fluent;
using Shared.DistributedKeys;

namespace Service1.Contract.Responses;

public class MemberResponse
{
    public string Id { get; set; }
    public string MemberAddressId { get; set; }
    public string MemberProvinceId { get; set; }
    public string MemberProvinceName { get; set; }
    public string MemberAdditionalId { get; set; }
    public string MemberAdditionalName { get; set; }
    public string MemberSocialId { get; set; }
    public string MemberSocialName { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string UserEmail { get; set; }
    public string ProvinceId { get; set; }
    public string ProvinceName { get; set; }
    public string CountryName { get; set; }
    public string CountryId { get; set; }
    public List<ProvinceResponse> Provinces { get; set; }
    public ProvinceResponse Province { get; set; }
}

public class MemberResponseProfile : ProfileOf<MemberResponse>
{
    protected override void Configure()
    {
        UseDistributedKey<MemberAddressDistributedKey>()
            .Of(x => x.MemberAddressId)
            .For(x => x.MemberProvinceId);

        UseDistributedKey<ProvinceDistributedKey>()
            .Of(x => x.MemberProvinceId)
            .For(x => x.MemberProvinceName);

        UseDistributedKey<MemberAdditionalDistributedKey>()
            .Of(x => x.MemberAdditionalId)
            .For(x => x.MemberAdditionalName);

        UseDistributedKey<MemberSocialDistributedKey>()
            .Of(x => x.MemberSocialId)
            .For(x => x.MemberSocialName);
        
        UseDistributedKey<UserDistributedKey>()
            .Of(x => x.UserId)
            .For(x => x.UserName)
            .For(x => x.UserEmail, "UserEmail")
            .For(x => x.ProvinceId, "ProvinceId");

        UseDistributedKey<ProvinceDistributedKey>()
            .Of(x => x.ProvinceId)
            .For(x => x.ProvinceName)
            .For(x => x.CountryName, "Country.Name")
            .For(x => x.CountryId, "CountryId");

        UseDistributedKey<CountryDistributedKey>()
            .Of(x => x.CountryId)
            .For(x => x.Provinces, "Provinces[asc Name]")
            .For(x => x.Province, "Provinces[0 asc Name]");
    }
}