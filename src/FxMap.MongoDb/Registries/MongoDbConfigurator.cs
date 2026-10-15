using FxMap.MongoDb.Abstractions;
using FxMap.MongoDb.Implementations;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace FxMap.MongoDb.Registries;

/// <summary>
/// Configuration class for registering MongoDB collections with the FxMap framework.
/// </summary>
/// <param name="serviceCollection">The service collection for dependency injection registration.</param>
/// <remarks>
/// Use this registrar to add MongoDB collections that FxMap will query for data.
/// Each collection is registered as a singleton service.
/// </remarks>
internal sealed class MongoDbConfigurator(IServiceCollection serviceCollection) : IMongoDbConfigurator
{
    /// <summary>
    /// Gets the types of models that have been registered with MongoDB collections.
    /// </summary>
    internal IReadOnlyCollection<Type> MongoModelTypes => _mongoModelTypes;
    private readonly List<Type> _mongoModelTypes = [];

    internal bool IsolatesExpressionErrors { get; private set; }

    /// <summary>
    /// When a request holds an expression that cannot be built (a syntax error, an unknown function...), answer the other
    /// expressions and report this one in <c>ItemsResponse.Errors</c>, instead of failing the whole request. The service that
    /// asked decides what to do with the error: it throws with <c>ThrowIfException()</c>, and logs a warning otherwise.
    /// </summary>
    public void IsolateExpressionErrors() => IsolatesExpressionErrors = true;

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
    public IMongoDbConfigurator AddCollection<TModel>(IMongoCollection<TModel> collection)
    {
        _mongoModelTypes.Add(typeof(TModel));
        serviceCollection.AddTransient<IMongoCollectionInternal<TModel>>(_ =>
            new MongoCollectionInternal<TModel>(collection));
        return this;
    }
}