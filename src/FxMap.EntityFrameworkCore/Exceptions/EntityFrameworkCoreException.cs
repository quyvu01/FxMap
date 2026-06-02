using FxMap.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FxMap.EntityFrameworkCore.Exceptions;

/// <summary>
/// Contains exception types specific to the FxMap Entity Framework Core integration.
/// </summary>
public static class EntityFrameworkCoreException
{
    /// <summary>
    /// Thrown when attempting to resolve a DbContext that has not been registered with the DI container.
    /// </summary>
    public class EntityFrameworkDbContextNotRegister() : Exception("DbContext must be registered first!");

    public class InputTypeIsNotDbContextType(Type type)
        : Exception($"The input type: {type.FullName} is not {nameof(DbContext)} type!");

    /// <summary>
    /// Thrown when no registered DbContext contains the requested entity model type.
    /// </summary>
    /// <param name="modelType">The model type that was not found in any DbContext.</param>
    public class ThereAreNoDbContextHasModel(Type modelType)
        : Exception($"There are no any db context contains model: {modelType.Name}");

    /// <summary>
    /// Thrown when AddFxMapEFCore is called without providing any DbContext types.
    /// </summary>
    public class DbContextsMustNotBeEmpty()
        : Exception(
            $"There are no any db contexts on {nameof(EntityFrameworkExtensions.AddEntityFrameworkCore)}() method");
}