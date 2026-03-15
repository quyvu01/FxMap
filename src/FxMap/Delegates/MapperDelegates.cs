using FxMap.Abstractions;
using FxMap.Accessors.TypeAccessors;
using FxMap.Fluent;

namespace FxMap.Delegates;

/// <summary>
/// Delegate for retrieving the FxMap configuration for a specific model and distributed key type combination.
/// </summary>
/// <param name="modelType">The CLR type of the model entity.</param>
/// <param name="distributedKeyType">The type of the <see cref="IDistributedKey"/>.</param>
/// <returns>
/// The <see cref="MapEntityConfig"/> containing the ID and default property configuration.
/// </returns>
public delegate MapEntityConfig MapperDelegates(Type modelType, Type distributedKeyType);

public delegate IFluentProfileConfig GetProfileConfig(Type profileType);

public delegate IFluentEntityConfig GetEntityConfig(Type entityType);

public delegate ITypeAccessor GetTypeAccessor(Type type);