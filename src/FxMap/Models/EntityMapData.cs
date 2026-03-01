using FxMap.Abstractions;

namespace FxMap.Models;

/// <summary>
/// Represents the complete metadata for an FxMap model, including its CLR type,
/// associated attribute type, and configuration.
/// </summary>
/// <param name="ModelType">
/// The CLR type of the model entity (e.g., <c>typeof(User)</c>, <c>typeof(Order)</c>).
/// </param>
/// <param name="DistributedKeyType">
/// The type of <see cref="IDistributedKey"/> associated with this model
/// (e.g., <c>typeof(UserOfAttribute)</c>).
/// </param>
/// <param name="MapEntityConfig">
/// The configuration attribute that defines the ID and default property mappings for this model.
/// </param>
public sealed record EntityMapData(Type ModelType, Type DistributedKeyType, MapEntityConfig MapEntityConfig);
