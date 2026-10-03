using FxMap.Abstractions;

namespace Shared.DistributedKeys;

/// <summary>Looks up items by Code. Code is NOT unique: several items can share one code.</summary>
public sealed class ItemDistributedKey : IDistributedKey;
