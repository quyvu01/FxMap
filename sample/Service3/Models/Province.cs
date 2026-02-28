using OfX.Fluent;
using Shared.Attributes;

namespace Service3Api.Models;

public sealed class Province
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string CountryId { get; set; }
    public Country Country { get; set; }
}

public class ProvinceConfig : AbstractOfXConfig<Province>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<ProvinceOfAttribute>();
    }
}