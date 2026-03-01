using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using FxMap.Abstractions;
using FxMap.Exceptions;

namespace FxMap.Helpers;

/// <summary>
/// Factory for creating or resolving <see cref="IDistributedKey"/> types from string keys.
/// When a string key is used instead of a concrete type, this factory generates a dynamic type
/// via IL emit that implements <see cref="IDistributedKey"/>.
/// </summary>
internal static partial class DistributedKeyTypeFactory
{
    private static readonly ConcurrentDictionary<string, Type> GeneratedTypes = new();

    private static readonly Lazy<ModuleBuilder> DynamicModule = new(() =>
    {
        var assemblyName = new AssemblyName("FxMap.DynamicDistributedKeys");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        return assemblyBuilder.DefineDynamicModule("MainModule");
    });

    /// <summary>
    /// Resolves a <see cref="Type"/> for a distributed key.
    /// If <paramref name="distributedKeyType"/> is provided, returns it directly.
    /// If <paramref name="distributedKey"/> (string) is provided, generates or retrieves a cached dynamic type.
    /// </summary>
    /// <exception cref="FxMapException.InvalidDistributedKeyConfiguration">
    /// Thrown when both or neither of the parameters are set.
    /// </exception>
    /// <exception cref="FxMapException.InvalidDistributedKeyName">
    /// Thrown when the string key contains invalid characters.
    /// </exception>
    internal static Type Resolve(Type distributedKeyType, string distributedKey)
    {
        var hasType = distributedKeyType is not null;
        var hasKey = !string.IsNullOrWhiteSpace(distributedKey);

        if (hasType == hasKey)
            throw new FxMapException.InvalidDistributedKeyConfiguration(distributedKeyType, distributedKey);

        return hasType ? distributedKeyType : GetOrCreateType(distributedKey);
    }

    private static Type GetOrCreateType(string key)
    {
        ValidateKeyName(key);
        return GeneratedTypes.GetOrAdd(key, static k =>
        {
            var typeBuilder = DynamicModule.Value.DefineType(
                $"FxMap.DynamicKeys.{k}",
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
                null,
                [typeof(IDistributedKey)]);
            return typeBuilder.CreateType()!;
        });
    }

    private static void ValidateKeyName(string key)
    {
        if (!ValidKeyPattern().IsMatch(key))
            throw new FxMapException.InvalidDistributedKeyName(key);
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex ValidKeyPattern();
}
