using OfX.Fluent;
using OfX.Tests.TestData.Attributes;

namespace OfX.Tests.TestData.Models;

public class City
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProvinceId { get; set; } = string.Empty;
    public int Population { get; set; }
}

public class CityConfig : AbstractOfXConfig<City>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseAnnotate<CityOfAttribute>();
    }
}
