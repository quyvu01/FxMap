using FxMap.Abstractions;

namespace FxMap.Exceptions;

/// <summary>
/// Contains all custom exception types used by the FxMap framework.
/// </summary>
/// <remarks>
/// These exceptions provide detailed error messages for common configuration
/// and runtime issues encountered when using the FxMap framework.
/// </remarks>
public static class DistributedMapException
{
    public sealed class CurrentIdTypeWasNotSupported() :
        Exception("Current Id type was not supported. Create the IdConverter!");

    public sealed class TypeIsNotReceivedPipelineBehavior(Type type) :
        Exception($"{type.Name} must implement {typeof(IReceivedPipelineBehavior<>).FullName}!");

    public sealed class TypeIsNotSendPipelineBehavior(Type type) :
        Exception($"{type.Name} must implement {typeof(ISendPipelineBehavior<>).FullName}!");

    public sealed class TypeIsNotCustomExpressionPipelineBehavior(Type type) :
        Exception($"{type.Name} must implement {typeof(ICustomExpressionBehavior<>).FullName}!");

    public sealed class CannotFindHandlerForDistributedKey(Type type)
        : Exception($"Cannot find handler for DistributedKey type: {type.Name}!");

    public sealed class StronglyTypeConfigurationImplementationMustNotBeGeneric(Type type)
        : Exception($"Strongly type configuration implementation must not be generic type: {type.Name}!");

    public sealed class StronglyTypeConfigurationMustNotBeNull()
        : Exception("Strongly type Id configuration must not be null!");

    public sealed class DistributedKeyHasBeenConfiguredForModel(Type modelType, Type distributedKeyType)
        : Exception(
            $"DistributedKey: {distributedKeyType.FullName} has been configured for {modelType.FullName} at least twice!");

    public sealed class MaxNestingDepthReached()
        : Exception(
            "FxMap mapping engine has reached the maximum nesting depth! Use `SetMaxNestingDepth` to increase the limit.");

    public sealed class AddProfilesFromAssemblyContaining()
        : Exception(
            "You have to call the method: `AddProfilesFromAssemblyContaining<TAssembly>` to register entity configurations!");

    public sealed class ReceivedException(string message)
        : Exception($"{AppDomain.CurrentDomain.FriendlyName} : {message}");

    public sealed class CollectionFormatNotCorrected(string collectionPropertyName) : Exception($"""
         Collection data [{collectionPropertyName}] must be defined as 
         [OrderDirection OrderedProperty] or 
         [Offset Limit OrderDirection OrderedProperty] or 
         [0 OrderDirection OrderedProperty](First item) or 
         [-1 OrderDirection OrderedProperty](Last item)
         """);

    public sealed class CollectionIndexIncorrect(string indexAsString)
        : Exception($"First parameter [{indexAsString}] must be 0(First item) or -1(Last item).");

    public sealed class CollectionOrderDirectionIncorrect(string orderDirection)
        : Exception($"Second parameter [{orderDirection}] must be an ordered direction `ASC|DESC`");

    public sealed class NavigatorIncorrect(string navigator, string parentType)
        : Exception($"Object: '{parentType}' does not include navigator: {navigator}");

    public sealed class AmbiguousHandlers(Type interfaceType) :
        Exception($"Ambiguous handlers for interface '{interfaceType.FullName}'.");

    public sealed class NoHandlerForDistributedKey(Type type)
        : Exception($"There is no handler for '{type.FullName}'!");


    public sealed class DuplicatedNameByExposedName(Type type, string exposedName) : Exception(
        $"ExposedName: {exposedName} cannot be duplicated for type '{type.FullName}'.");

    public sealed class DistributedKeyAssignedToMultipleEntities(Type distributedKeyType, Type[] entityTypes)
        : Exception(
            $"DistributedKey: {distributedKeyType.FullName} has been assigned to multiple entities: {string.Join(", ", entityTypes.Select(t => t.FullName))}");

    public sealed class InvalidParameterType(string message) : Exception(message);

    public sealed class InvalidDistributedKeyConfiguration(Type distributedKeyType, string distributedKey)
        : Exception(distributedKeyType is not null && !string.IsNullOrWhiteSpace(distributedKey)
            ? $"Cannot set both DistributedKeyType ({distributedKeyType.FullName}) and DistributedKey (\"{distributedKey}\"). Use only one."
            : "Either DistributedKeyType or DistributedKey must be set. Both cannot be null.");

    public sealed class InvalidDistributedKeyName(string key)
        : Exception(
            $"DistributedKey \"{key}\" is invalid. Must start with a letter or underscore, and contain only letters, digits, or underscores (e.g., \"UserKey\", \"Order_Key\").");
}