using OfX.Fluent;
using Shared.Attributes;

namespace Service1.Contract.Responses;

public class ProvinceComplexResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string CountryName { get; set; }
    public string CountryId { get; set; }
    public List<SampleProvinceByCountryResponse> Provinces { get; set; }
}

public class ProvinceComplexResponseProfile : ProfileOf<ProvinceComplexResponse>
{
    protected override void Configure()
    {
        UseDistributedKey<CountryOfAttribute>()
            .Of(x => x.CountryId)
            .For(x => x.Provinces, "Provinces.{Id, (Name endswith '0' ? Name : 'N/A') as Name}");
    }
}

public class SampleProvinceByCountryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}