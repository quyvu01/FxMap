using FxMap.Configuration;
using FxMap.EntityFrameworkCore.Abstractions;
using FxMap.EntityFrameworkCore.Exceptions;
using FxMap.EntityFrameworkCore.Implementations;
using FxMap.Exceptions;
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
    private static readonly Dictionary<Type, string> DbContextMapFunction = [];

    /// <summary>
    /// Registers one or more DbContext types for use with FxMap queries.
    /// </summary>
    /// <param name="dbContextType">The primary DbContext type to register.</param>
    /// <param name="otherDbContextTypes">Additional DbContext types to register.</param>
    /// <exception cref="FxMapEntityFrameworkException.DbContextsMustNotBeEmpty">
    /// Thrown when no DbContext types are provided.
    /// </exception>
    /// <exception cref="FxMapEntityFrameworkException.DbContextTypeHasBeenRegisterBefore">
    /// Thrown when a DbContext type has already been registered.
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
        List<Type> dbContextTypes = [dbContextType, ..otherDbContextTypes ?? []];
        if (dbContextTypes.Count == 0)
            throw new FxMapEntityFrameworkException.DbContextsMustNotBeEmpty();

        if (!FxMapStatics.HasModelConfigurations)
            throw new FxMapException.AddProfilesFromAssemblyContaining();

        dbContextTypes.Distinct().ForEach(type =>
        {
            ArgumentNullException.ThrowIfNull(type);
            if (!DbContextMapFunction.TryAdd(type, nameof(AddDbContexts)))
                throw new FxMapEntityFrameworkException.DbContextTypeHasBeenRegisterBefore(type);
            serviceCollection.AddScoped<IDbContext>(sp => sp.GetService(type) is DbContext context
                ? new DbContextInternal(context)
                : throw new FxMapEntityFrameworkException.EntityFrameworkDbContextNotRegister());
        });
    }
}