using FxMap.Abstractions;
using FxMap.Fluent;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.Tests.UnitTests.Mapping;

// Profiles are INTERNAL on purpose: MapConfigurator.ScanProfileConfigs only scans exported types, so these never leak
// into the tests that call AddProfilesFromAssemblyContaining<ITestAssemblyMarker>().

#region Keys

public sealed class UserKey : IDistributedKey;

public sealed class ProvinceKey : IDistributedKey;

public sealed class CountryKey : IDistributedKey;

public sealed class ProductKey : IDistributedKey;

public sealed class CategoryKey : IDistributedKey;

#endregion

public enum Role
{
    None = 0,
    Admin = 1,
    Guest = 2
}

public sealed class ModeFlag
{
    public bool UseEmail { get; set; }
}

#region Scalars

public class FlatDto
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string UserEmail { get; set; }
    public int Age { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Active { get; set; }
    public Role Role { get; set; }
    public int? Score { get; set; }
    public string Untouched { get; set; } = "keep";
}

internal sealed class FlatDtoProfile : ProfileOf<FlatDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>()
            .Of(x => x.UserId)
            .For(x => x.UserName, "Name")
            .For(x => x.UserEmail, "Email")
            .For(x => x.Age, "Age")
            .For(x => x.Balance, "Balance")
            .For(x => x.CreatedAt, "CreatedAt")
            .For(x => x.Active, "Active")
            .For(x => x.Role, "Role")
            .For(x => x.Score, "Score");
}

public class GuidSelectorDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
}

internal sealed class GuidSelectorDtoProfile : ProfileOf<GuidSelectorDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.UserId).For(x => x.UserName, "Name");
}

public class IntSelectorDto
{
    public int UserId { get; set; }
    public string UserName { get; set; }
}

internal sealed class IntSelectorDtoProfile : ProfileOf<IntSelectorDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.UserId).For(x => x.UserName, "Name");
}

public class NullExpressionDto
{
    public string UserId { get; set; }
    public string UserName { get; set; }
}

internal sealed class NullExpressionDtoProfile : ProfileOf<NullExpressionDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.UserId).For(x => x.UserName);
}

/// <summary>Two selectors feeding the same key, with different expressions.</summary>
public class TwoSelectorsDto
{
    public string PrimaryUserId { get; set; }
    public string PrimaryUserName { get; set; }
    public string SecondaryUserId { get; set; }
    public string SecondaryUserEmail { get; set; }
}

internal sealed class TwoSelectorsDtoProfile : ProfileOf<TwoSelectorsDto>
{
    protected override void Configure()
    {
        UseDistributedKey<UserKey>().Of(x => x.PrimaryUserId).For(x => x.PrimaryUserName, "Name");
        UseDistributedKey<UserKey>().Of(x => x.SecondaryUserId).For(x => x.SecondaryUserEmail, "Email");
    }
}

#endregion

#region Dependency chains

/// <summary>UserId -> (UserName, ProvinceId); ProvinceId -> (ProvinceName, CountryId); CountryId -> CountryName.</summary>
public class ChainDto
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string ProvinceId { get; set; }
    public string ProvinceName { get; set; }
    public string CountryId { get; set; }
    public string CountryName { get; set; }
}

internal sealed class ChainDtoProfile : ProfileOf<ChainDto>
{
    protected override void Configure()
    {
        UseDistributedKey<UserKey>().Of(x => x.UserId)
            .For(x => x.UserName, "Name")
            .For(x => x.ProvinceId, "ProvinceId");
        UseDistributedKey<ProvinceKey>().Of(x => x.ProvinceId)
            .For(x => x.ProvinceName, "Name")
            .For(x => x.CountryId, "CountryId");
        UseDistributedKey<CountryKey>().Of(x => x.CountryId)
            .For(x => x.CountryName, "Name");
    }
}

/// <summary>Two independent order-0 keys (User, Product) with an order-1 dependent each.</summary>
public class DiamondDto
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string ProvinceId { get; set; }
    public string ProvinceName { get; set; }
    public string ProductId { get; set; }
    public string ProductName { get; set; }
    public string CategoryId { get; set; }
    public string CategoryName { get; set; }
}

internal sealed class DiamondDtoProfile : ProfileOf<DiamondDto>
{
    protected override void Configure()
    {
        UseDistributedKey<UserKey>().Of(x => x.UserId)
            .For(x => x.UserName, "Name")
            .For(x => x.ProvinceId, "ProvinceId");
        UseDistributedKey<ProvinceKey>().Of(x => x.ProvinceId).For(x => x.ProvinceName, "Name");
        UseDistributedKey<ProductKey>().Of(x => x.ProductId)
            .For(x => x.ProductName, "Name")
            .For(x => x.CategoryId, "CategoryId");
        UseDistributedKey<CategoryKey>().Of(x => x.CategoryId).For(x => x.CategoryName, "Name");
    }
}

#endregion

#region Object graphs

public class ItemDto
{
    public string ProductId { get; set; }
    public string ProductName { get; set; }
    public string CategoryId { get; set; }
    public string CategoryName { get; set; }
}

internal sealed class ItemDtoProfile : ProfileOf<ItemDto>
{
    protected override void Configure()
    {
        UseDistributedKey<ProductKey>().Of(x => x.ProductId)
            .For(x => x.ProductName, "Name")
            .For(x => x.CategoryId, "CategoryId");
        UseDistributedKey<CategoryKey>().Of(x => x.CategoryId).For(x => x.CategoryName, "Name");
    }
}

/// <summary>Every kind of container the walker has to understand.</summary>
public class OrderDto
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public List<ItemDto> Items { get; set; } = [];
    public ItemDto Main { get; set; }
    public ItemDto[] Array { get; set; }
    public Dictionary<string, ItemDto> ByCode { get; set; }
    public IEnumerable<ItemDto> Lazy { get; set; }
}

internal sealed class OrderDtoProfile : ProfileOf<OrderDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.UserId).For(x => x.UserName, "Name");
}

/// <summary>Self-referencing model: linked list + tree + arbitrary graph.</summary>
public class Node
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string ProvinceId { get; set; }
    public string ProvinceName { get; set; }
    public Node Next { get; set; }
    public Node Left { get; set; }
    public Node Right { get; set; }
    public List<Node> Children { get; set; } = [];
    public Dictionary<string, Node> Named { get; set; } = new();
}

internal sealed class NodeProfile : ProfileOf<Node>
{
    protected override void Configure()
    {
        UseDistributedKey<UserKey>().Of(x => x.UserId)
            .For(x => x.UserName, "Name")
            .For(x => x.ProvinceId, "ProvinceId");
        UseDistributedKey<ProvinceKey>().Of(x => x.ProvinceId).For(x => x.ProvinceName, "Name");
    }
}

/// <summary>A holder with no rules of its own (virtual profile) that only contains mappable objects.</summary>
public class HolderDto
{
    public string Name { get; set; }
    public BaseItem Item { get; set; }
    public List<BaseItem> Items { get; set; } = [];
    public object Boxed { get; set; }
}

public class BaseItem
{
    public string Tag { get; set; }
}

public class DerivedItem : BaseItem
{
    public string UserId { get; set; }
    public string UserName { get; set; }
}

internal sealed class DerivedItemProfile : ProfileOf<DerivedItem>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.UserId).For(x => x.UserName, "Name");
}

/// <summary>Things that are "not primitive" for FxMap but should never be walked into.</summary>
public class BlobDto
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public byte[] Payload { get; set; }
    public List<string> Tags { get; set; }
    public Dictionary<string, string> Labels { get; set; }
    public int[] Numbers { get; set; }
    public Uri Link { get; set; }
}

internal sealed class BlobDtoProfile : ProfileOf<BlobDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.UserId).For(x => x.UserName, "Name");
}

#endregion

/// <summary>Every collection shape that still contains models and must therefore be walked.</summary>
public class CollectionsDto
{
    public Dictionary<string, List<ItemDto>> Grouped { get; set; }
    public List<List<ItemDto>> Matrix { get; set; }
    public ItemDto[][] Jagged { get; set; }
    public IEnumerable<object> Mixed { get; set; }
    public object Boxed { get; set; }
    public Dictionary<string, object> Bag { get; set; }
    public IReadOnlyDictionary<string, ItemDto> ReadOnly { get; set; }
    public IList<ItemDto> AsInterface { get; set; }
}

/// <summary>Counts how often someone enumerates it, to prove the walker leaves leaf collections alone.</summary>
public sealed class CountingStrings : IEnumerable<string>
{
    public int Enumerations { get; private set; }

    public IEnumerator<string> GetEnumerator()
    {
        Enumerations++;
        yield return "a";
        yield return "b";
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public class LeafHolderDto
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public IEnumerable<string> Names { get; set; }
    public CountingStrings Counting { get; set; }
    public object Boxed { get; set; }
    public List<object> BoxedList { get; set; }
}

internal sealed class LeafHolderDtoProfile : ProfileOf<LeafHolderDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.UserId).For(x => x.UserName, "Name");
}

#region Mapped values that are objects themselves (multi level)

public class AddressDto
{
    public string ProvinceId { get; set; }
    public string ProvinceName { get; set; }
}

internal sealed class AddressDtoProfile : ProfileOf<AddressDto>
{
    protected override void Configure() =>
        UseDistributedKey<ProvinceKey>().Of(x => x.ProvinceId).For(x => x.ProvinceName, "Name");
}

/// <summary>Address/Addresses are filled with whole objects by the remote, which then need their own mapping.</summary>
public class ProfileDto
{
    public string UserId { get; set; }
    public AddressDto Address { get; set; }
    public List<AddressDto> Addresses { get; set; }
}

internal sealed class ProfileDtoProfile : ProfileOf<ProfileDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.UserId)
            .For(x => x.Address, "Address")
            .For(x => x.Addresses, "Addresses");
}

#endregion

#region Conditional expressions

public class ConditionalDto
{
    public string UserId { get; set; }
    public string UserName { get; set; }
}

internal sealed class ConditionalDtoProfile : ProfileOf<ConditionalDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.UserId)
            .For(x => x.UserName, c => c
                .If(sp => sp.GetRequiredService<ModeFlag>().UseEmail)
                .Expression("Email")
                .Else("Name"));
}

#endregion

/// <summary>Random graph used by the oracle based test.</summary>
public class GNode
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string ProvinceId { get; set; }
    public string ProvinceName { get; set; }
    public GNode Left { get; set; }
    public GNode Right { get; set; }
    public List<GNode> Children { get; set; } = [];
    public Dictionary<string, GNode> Named { get; set; } = new();
}

internal sealed class GNodeProfile : ProfileOf<GNode>
{
    protected override void Configure()
    {
        UseDistributedKey<UserKey>().Of(x => x.UserId)
            .For(x => x.UserName, "Name")
            .For(x => x.ProvinceId, "ProvinceId");
        UseDistributedKey<ProvinceKey>().Of(x => x.ProvinceId).For(x => x.ProvinceName, "Name");
    }
}

#region Collections: one element per row of a key that matches several rows

public class RowItem
{
    public string Name { get; set; }
    public string Email { get; set; }
    public int RowId { get; set; }
    public string ProvinceId { get; set; }
    public string ProvinceName { get; set; }
}

/// <summary>Elements that need a second round of mapping (ProvinceName comes from the ProvinceId of the row).</summary>
internal sealed class RowItemProfile : ProfileOf<RowItem>
{
    protected override void Configure() =>
        UseDistributedKey<ProvinceKey>().Of(x => x.ProvinceId).For(x => x.ProvinceName, "Name");
}

public class RowsDto
{
    public string Code { get; set; }
    public string Name { get; set; }
    public List<RowItem> Items { get; set; }
}

internal sealed class RowsDtoProfile : ProfileOf<RowsDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.Code)
            .For(x => x.Name, "Name")
            .Collection(x => x.Items, i => i
                .For(x => x.Name, "Name")
                .For(x => x.Email, "Email")
                .For(x => x.RowId, "Id")
                .For(x => x.ProvinceId, "ProvinceId"));
}

public class RowsArrayDto
{
    public string Code { get; set; }
    public RowItem[] Items { get; set; }
}

internal sealed class RowsArrayDtoProfile : ProfileOf<RowsArrayDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.Code)
            .Collection(x => x.Items, i => i.For(x => x.Name, "Name").For(x => x.RowId, "Id").Limit(2));
}

public class RowsInterfaceDto
{
    public string Code { get; set; }
    public IReadOnlyList<RowItem> ReadOnly { get; set; }
    public IEnumerable<RowItem> Enumerable { get; set; }
    public ICollection<RowItem> Collection { get; set; }
}

internal sealed class RowsInterfaceDtoProfile : ProfileOf<RowsInterfaceDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.Code)
            .Collection(x => x.ReadOnly, i => i.For(x => x.Name, "Name"))
            .Collection(x => x.Enumerable, i => i.For(x => x.RowId, "Id"))
            .Collection(x => x.Collection, i => i.For(x => x.Email, "Email"));
}

public class RowsConditionalDto
{
    public string Code { get; set; }
    public List<RowItem> Items { get; set; }
}

internal sealed class RowsConditionalDtoProfile : ProfileOf<RowsConditionalDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.Code)
            .Collection(x => x.Items, i => i.For(x => x.Name, c => c
                .If(sp => sp.GetRequiredService<ModeFlag>().UseEmail)
                .Expression("Email")
                .Else("Name")));
}

/// <summary>The collection is at order 1: its selector (Code) is filled by another key first.</summary>
public class RowsChainDto
{
    public string UserId { get; set; }
    public string Code { get; set; }
    public List<RowItem> Items { get; set; }
}

internal sealed class RowsChainDtoProfile : ProfileOf<RowsChainDto>
{
    protected override void Configure()
    {
        UseDistributedKey<UserKey>().Of(x => x.UserId).For(x => x.Code, "Code");
        UseDistributedKey<ProductKey>().Of(x => x.Code)
            .Collection(x => x.Items, i => i.For(x => x.Name, "Name").For(x => x.RowId, "Id"));
    }
}

#endregion

#region Collections with options (order / limit)

public class RowsOptionsDto
{
    public string Code { get; set; }
    public List<RowItem> Recent { get; set; }
    public List<RowItem> Oldest { get; set; }
    public List<RowItem> All { get; set; }
    public string Name { get; set; }
}

internal sealed class RowsOptionsDtoProfile : ProfileOf<RowsOptionsDto>
{
    protected override void Configure() =>
        UseDistributedKey<UserKey>().Of(x => x.Code)
            .Collection(x => x.Recent, i => i.For(x => x.Name, "Name").For(x => x.RowId, "Id")
                .OrderByDescending("Date").ThenBy("Id").Limit(2))
            .Collection(x => x.Oldest, i => i.For(x => x.Name, "Name").For(x => x.RowId, "Id")
                .OrderBy("Date").Limit(1))
            .Collection(x => x.All, i => i.For(x => x.RowId, "Id"))
            .For(x => x.Name, "Name");
}

#endregion
