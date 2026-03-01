using MongoDB.Bson.Serialization.Attributes;
using FxMap.Fluent;
using Shared.Attributes;

namespace Service1.Models;

public sealed class MemberSocial
{
    [BsonId] public int Id { get; set; }
    public string Name { get; set; }
    public string OtherValue { get; set; }
    public DateTime CreatedTime { get; set; }
    public List<MemerSocialMetadata> Metadata { get; set; }
}

public sealed class MemerSocialMetadata
{
    public string Key { get; set; }
    public string Value { get; set; }
    public int Order { get; set; }
    public ExternalOfMetadata ExternalOfMetadata { get; set; }
}

public sealed class ExternalOfMetadata
{
    public string JustForTest { get; set; }
}

public class MemberSocialConfig : EntityConfigureOf<MemberSocial>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<MemberSocialOfAttribute>();
    }
}