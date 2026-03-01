using FxMap.Abstractions;

namespace FxMap.Delegates;

/// <summary>
/// Delegate for retrieving the FxMap configuration for a specific model and distributed key type combination.
/// </summary>
/// <param name="modelType">The CLR type of the model entity.</param>
/// <param name="distributedKeyType">The type of the <see cref="IDistributedKey"/>.</param>
/// <returns>
/// The <see cref="MapEntityConfig"/> containing the ID and default property configuration.
/// </returns>
public delegate MapEntityConfig GetFxMapConfiguration(Type modelType, Type distributedKeyType);