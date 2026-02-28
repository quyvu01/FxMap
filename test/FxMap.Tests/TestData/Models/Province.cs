using FxMap.Fluent;
using FxMap.Tests.TestData.Attributes;

namespace FxMap.Tests.TestData.Models;

public class Province
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public Country Country { get; set; }
    public List<City> Cities { get; set; } = [];
}

public class ProvinceConfig : AbstractFxMapConfig<Province>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<ProvinceOfAttribute>();
    }
}
