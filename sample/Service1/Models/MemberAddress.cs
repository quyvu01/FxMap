using FxMap.Fluent;
using Shared.Attributes;

namespace Service1.Models;

public sealed class MemberAddress
{
    public string Id { get; set; }
    public string ProvinceId { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string ZipCode { get; set; }
}

public class MemberAddressConfig : EntityConfigureOf<MemberAddress>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.ProvinceId);
        UseDistributedKey<MemberAddressOfAttribute>();
    }
}