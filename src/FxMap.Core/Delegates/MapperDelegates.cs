using FxMap.Abstractions;
using FxMap.Accessors.TypeAccessors;
using FxMap.Fluent;
using FxMap.Responses;

namespace FxMap.Delegates;

/// <summary>
/// Delegate for retrieving the FxMap configuration for a specific model and distributed key type combination.
/// </summary>
/// <param name="modelType">The CLR type of the model entity.</param>
/// <param name="distributedKeyType">The type of the <see cref="IDistributedKey"/>.</param>
/// <returns>
/// The <see cref="IMapEntityConfig"/> containing the ID and default property configuration.
/// </returns>
internal delegate IMapEntityConfig MapperDelegates(Type modelType, Type distributedKeyType);

internal delegate IFluentProfileConfig GetProfileConfig(Type profileType);

internal delegate IFluentEntityConfig GetEntityConfig(Type entityType);

internal delegate ITypeAccessor GetTypeAccessor(Type type);

public delegate Task<ItemsResponse<DataResponse>> SendHandlerDelegate();

public delegate Task<ItemsResponse<DataResponse>> ReceivedHandlerDelegate();