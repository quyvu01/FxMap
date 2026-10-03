using FxMap.Fluent;
using Shared.DistributedKeys;

namespace Service5.Responses;

public class ItemResponse
{
    public int ItemId { get; set; }
    public string Name { get; set; }
}

/// <summary>Items grouped by Code: code A matches one row, code B matches two, an unknown code matches none.</summary>
public class ItemGroupResponse
{
    public string Code { get; set; } = "";

    /// <summary>Plain rule: the first row of the code.</summary>
    public string Name { get; set; }

    public List<ItemResponse> Items { get; set; }
}

public class ItemGroupResponseProfile : ProfileOf<ItemGroupResponse>
{
    protected override void Configure() =>
        UseDistributedKey<ItemDistributedKey>()
            .Of(x => x.Code)
            .For(x => x.Name, "Name")
            .Collection(x => x.Items, i => i
                .For(x => x.ItemId, "Id")
                .For(x => x.Name, "Name"));
}
