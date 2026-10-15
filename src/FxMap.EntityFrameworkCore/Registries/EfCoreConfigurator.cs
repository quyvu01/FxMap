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
internal sealed class EfCoreConfigurator(IServiceCollection serviceCollection) : IEfCoreConfigurator
{
    private readonly HashSet<Type> _dbContextTypes = [];
    internal bool IsolatesExpressionErrors { get; private set; }

    /// <summary>
    /// When a request holds an expression that cannot be built (an unknown function, an unknown property...), answer the
    /// other expressions and report this one in <c>ItemsResponse.Errors</c>, instead of failing the whole request. The service
    /// that asked decides what to do with the error: it throws with <c>ThrowIfException()</c>, and logs a warning otherwise.
    /// An error raised by the database when the query runs (a LINQ expression that EF Core cannot translate) still fails the
    /// request.
    /// </summary>
    public void IsolateExpressionErrors() => IsolatesExpressionErrors = true;

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
    public void AddDbContexts(Type dbContextType, params Type[] otherDbContextTypes) =>
        AddDbContexts([dbContextType, .. otherDbContextTypes ?? []]);

    /// <summary>
    /// Registers one or more DbContext types for use with FxMap queries.
    /// </summary>
    /// <param name="dbContextTypes">Add DbContext types to register.</param>
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
    public void AddDbContexts(params Type[] dbContextTypes) => AddDbContexts((IEnumerable<Type>)dbContextTypes);

    /// <summary>
    /// Registers one or more DbContext types for use with FxMap queries.
    /// </summary>
    /// <param name="dbContextTypes">Add DbContext types to register.</param>
    /// <exception cref="EntityFrameworkCoreException.DbContextsMustNotBeEmpty">
    /// Thrown when no DbContext types are provided.
    /// </exception>
    /// <example>
    /// <code>
    /// .AddFxMapEFCore(cfg =>
    /// {
    ///     cfg.AddDbContexts([typeof(ApplicationDbContext), typeof(ReportingDbContext)]);
    /// });
    /// </code>
    /// </example>
    public void AddDbContexts(IEnumerable<Type> dbContextTypes)
    {
        foreach (var dbContextType in dbContextTypes)
        {
            ArgumentNullException.ThrowIfNull(dbContextType);
            _dbContextTypes.Add(dbContextType);
        }
    }

    internal void RegisterDbContexts()
    {
        if (_dbContextTypes.Count == 0)
            throw new EntityFrameworkCoreException.DbContextsMustNotBeEmpty();
        _dbContextTypes.ForEach(runtimeType =>
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