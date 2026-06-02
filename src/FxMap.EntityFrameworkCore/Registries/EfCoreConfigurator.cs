using FxMap.EntityFrameworkCore.Abstractions;
using FxMap.EntityFrameworkCore.Exceptions;
using FxMap.EntityFrameworkCore.Implementations;
using FxMap.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.EntityFrameworkCore.Registries;

/// <summary>
/// Configuration class for registering Entity Framework Core DbContexts with the FxMap framework.
/// </summary>
/// <param name="serviceCollection">The service collection for dependency injection registration.</param>
/// <remarks>
/// This registrar supports multiple DbContexts, which is useful for applications with
/// multiple databases or bounded contexts. FxMap will automatically route queries to
/// the correct DbContext based on which one contains the target entity type.
/// </remarks>
public sealed class EfCoreConfigurator(IServiceCollection serviceCollection)
{
    /// <summary>
    /// Registers one or more DbContext types for use with FxMap queries.
    /// </summary>
    /// <param name="dbContextType">The primary DbContext type to register.</param>
    /// <param name="otherDbContextTypes">Additional DbContext types to register.</param>
    /// <exception cref="EntityFrameworkCoreException.DbContextsMustNotBeEmpty">
    /// Thrown when no DbContext types are provided.
    /// </exception>
    /// <example>
    /// <code>
    /// .AddFxMapEFCore(cfg =>
    /// {
    ///     cfg.AddDbContexts(typeof(ApplicationDbContext), typeof(ReportingDbContext));
    /// });
    /// </code>
    /// </example>
    public void AddDbContexts(Type dbContextType, params Type[] otherDbContextTypes)
    {
        var dbContextTypes = new HashSet<Type>([dbContextType, ..otherDbContextTypes ?? []]);
        if (dbContextTypes.Count == 0)
            throw new EntityFrameworkCoreException.DbContextsMustNotBeEmpty();

        dbContextTypes.ForEach(runtimeType =>
        {
            ArgumentNullException.ThrowIfNull(runtimeType);
            if (!typeof(DbContext).IsAssignableFrom(runtimeType))
                throw new EntityFrameworkCoreException.InputTypeIsNotDbContextType(runtimeType);
            serviceCollection.AddScoped<IDbContext>(sp => sp.GetService(runtimeType) is DbContext context
                ? new DbContextInternal(context)
                : throw new EntityFrameworkCoreException.EntityFrameworkDbContextNotRegister());
        });
    }
}