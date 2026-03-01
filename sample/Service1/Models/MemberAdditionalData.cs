using FxMap.Fluent;
using Shared.Attributes;

namespace Service1.Models;

public class MemberAdditionalData
{
    public string Id { get; set; }
    public string Name { get; set; }
}

public class MemberAdditionalDataConfig : EntityConfigureOf<MemberAdditionalData>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<MemberAdditionalOfAttribute>();
    }
}