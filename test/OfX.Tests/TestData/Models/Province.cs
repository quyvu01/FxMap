using OfX.Fluent;
using OfX.Tests.TestData.Attributes;

namespace OfX.Tests.TestData.Models;

public class Province
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public Country Country { get; set; }
    public List<City> Cities { get; set; } = [];
}

public class ProvinceConfig : AbstractOfXConfig<Province>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseAnnotate<ProvinceOfAttribute>();
    }
}
