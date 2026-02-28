using Microsoft.EntityFrameworkCore;
using FxMap.EntityFrameworkCore.Abstractions;

namespace FxMap.EntityFrameworkCore.Implementations;

/// <summary>
/// Internal implementation of <see cref="IDbContext"/> that wraps an Entity Framework DbContext.
/// </summary>
/// <param name="dbContext">The underlying EF Core DbContext.</param>
internal sealed class DbContextInternal(DbContext dbContext) : IDbContext
{
    /// <inheritdoc />
    public DbContext DbContext => dbContext;

    /// <inheritdoc />
    public bool HasCollection(Type modelType) => dbContext.Model.FindEntityType(modelType) is not null;
}