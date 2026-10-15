using FxMap.MongoDb.Abstractions;
using FxMap.MongoDb.Implementations;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace FxMap.MongoDb.Registries;

public interface IMongoDbConfigurator
{
    /// <summary>
    /// When a request holds an expression that cannot be built (a syntax error, an unknown function...), answer the other
    /// expressions and report this one in <c>ItemsResponse.Errors</c>, instead of failing the whole request. The service that
    /// asked decides what to do with the error: it throws with <c>ThrowIfException()</c>, and logs a warning otherwise.
    /// </summary>
    void IsolateExpressionErrors();

    /// <summary>
    /// Registers a MongoDB collection for use with FxMap queries.
    /// </summary>
    /// <typeparam name="TModel">The document type of the collection.</typeparam>
    /// <param name="collection">The MongoDB collection instance.</param>
    /// <returns>This registrar for method chaining.</returns>
    /// <example>
    /// <code>
    /// .AddMongoDb(cfg =>
    /// {
    ///     cfg.AddCollection(mongoDatabase.GetCollection&lt;User&gt;("users"));
    ///     cfg.AddCollection(mongoDatabase.GetCollection&lt;Product&gt;("products"));
    /// });
    /// </code>
    /// </example>
    IMongoDbConfigurator AddCollection<TModel>(IMongoCollection<TModel> collection);
}
