using FxMap.Fluent;
using Shared.Attributes;

namespace Service1.Contract.Responses;

public class ProvinceResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public CountryResponse Country { get; set; }
}

public class ProvinceResponseProfile : ProfileOf<ProvinceResponse>
{
    protected override void Configure()
    {
        UseDistributedKey<ProvinceOfAttribute>()
            .Of(x => x.Id)
            .For(x => x.Country, "Country.{Id, Name}");
    }
}