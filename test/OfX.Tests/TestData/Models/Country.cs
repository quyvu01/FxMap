using OfX.Fluent;
using OfX.Tests.TestData.Attributes;

namespace OfX.Tests.TestData.Models;

public class Country
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public List<Province> Provinces { get; set; } = [];
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
