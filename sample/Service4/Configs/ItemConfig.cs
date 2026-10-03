using FxMap.Fluent;
using Service4.Models;
using Shared.DistributedKeys;

namespace Service4.Configs;

/// <summary>Items are looked up by Code, which several rows share.</summary>
public class ItemConfig : EntityConfigureOf<Item>
{
    protected override void Configure()
    {
        Id(x => x.Code);
        DefaultProperty(x => x.Name);
        UseDistributedKey<ItemDistributedKey>();
    }
}
