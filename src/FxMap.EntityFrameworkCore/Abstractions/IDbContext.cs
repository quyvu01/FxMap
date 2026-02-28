using Microsoft.EntityFrameworkCore;

namespace FxMap.EntityFrameworkCore.Abstractions;

/// <summary>
/// Internal interface for abstracting DbContext access in the FxMap Entity Framework Core integration.
/// </summary>
/// <remarks>
/// This interface allows FxMap to work with multiple DbContext instances and query the correct one
/// based on which context contains a given entity type.
/// </remarks>
internal interface IDbContext
{
    /// <summary>
    /// Gets the underlying Entity Framework DbContext.
    /// </summary>
    DbContext DbContext { get; }

    /// <summary>
    /// Determines if this DbContext contains a DbSet for the specified model type.
    /// </summary>
    /// <param name="modelType">The entity type to check for.</param>
    /// <returns>True if the DbContext has this entity type registered; otherwise false.</returns>
    bool HasCollection(Type modelType);
}