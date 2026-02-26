using OfX.Fluent;
using Shared.Attributes;

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
        UseAnnotate<MemberAddressOfAttribute>()
            .Of(x => x.MemberAddressId)
            .For(x => x.MemberProvinceId);

        UseAnnotate<ProvinceOfAttribute>()
            .Of(x => x.MemberProvinceId)
            .For(x => x.MemberProvinceName);

        UseAnnotate<MemberAdditionalOfAttribute>()
            .Of(x => x.MemberAdditionalId)
            .For(x => x.MemberAdditionalName);

        UseAnnotate<MemberSocialOfAttribute>()
            .Of(x => x.MemberSocialId)
            .For(x => x.MemberSocialName);

        UseAnnotate<UserOfAttribute>()
            .Of(x => x.UserId)
            .For(x => x.UserName)
            .For(x => x.UserEmail, "${UserAlias|UserEmail}")
            .For(x => x.ProvinceId, "ProvinceId");

        UseAnnotate<ProvinceOfAttribute>()
            .Of(x => x.ProvinceId)
            .For(x => x.ProvinceName)
            .For(x => x.CountryName, "Country.Name")
            .For(x => x.CountryId, "CountryId");

        UseAnnotate<CountryOfAttribute>()
            .Of(x => x.CountryId)
            .For(x => x.Provinces, "Provinces[asc Name]")
            .For(x => x.Province, "Provinces[${index|0} ${order|asc} Name]");
    }
}