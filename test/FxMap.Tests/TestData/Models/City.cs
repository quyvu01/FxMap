using FxMap.Fluent;
using FxMap.Tests.TestData.Attributes;

namespace FxMap.Tests.TestData.Models;

public class City
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProvinceId { get; set; } = string.Empty;
    public int Population { get; set; }
}

public class CityConfig : EntityConfigureOf<City>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<CityOfAttribute>();
    }
}
