using OfX.Fluent;
using Shared.Attributes;

namespace Service1.Contract.Responses;

public class CountryResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string FirstProvinceName { get; set; }
}

public class CountryResponseProfile : ProfileOf<CountryResponse>
{
    protected override void Configure()
    {
        UseDistributedKey<CountryOfAttribute>()
            .Of(x => x.Id)
            .For(x => x.FirstProvinceName, "Provinces(Name endswith 'a')[0 desc Name].Name");
    }
}