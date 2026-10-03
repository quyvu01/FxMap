using FxMap.Abstractions;

namespace Shared.DistributedKeys;

/// <summary>Looks up visits by Mrn. Mrn is NOT unique: a patient has many visits.</summary>
public sealed class VisitDistributedKey : IDistributedKey;
