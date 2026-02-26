using OfX.Fluent;
using Shared.Attributes;

namespace Service3Api.Models;

public class Country
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<Province> Provinces { get; set; }
}

public class CountryConfig : AbstractOfXConfig<Country>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseAnnotate<CountryOfAttribute>();
    }
}