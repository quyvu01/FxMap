using FxMap.EntityFrameworkCore.Abstractions;
using FxMap.EntityFrameworkCore.Exceptions;
using FxMap.EntityFrameworkCore.Implementations;
using FxMap.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FxMap.EntityFrameworkCore.Registries;

public interface IEfCoreConfigurator
{
    /// <summary>
    /// When a request holds an expression that cannot be built (an unknown function, an unknown property...), answer the
    /// other expressions and report this one in <c>ItemsResponse.Errors</c>, instead of failing the whole request. The service
    /// that asked decides what to do with the error: it throws with <c>ThrowIfException()</c>, and logs a warning otherwise.
    /// An error raised by the database when the query runs (a LINQ expression that EF Core cannot translate) still fails the
    /// request.
    /// </summary>
    void IsolateExpressionErrors();

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
    void AddDbContexts(Type dbContextType, params Type[] otherDbContextTypes);

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
    void AddDbContexts(params Type[] dbContextTypes);

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
    void AddDbContexts(IEnumerable<Type> dbContextTypes);
}
