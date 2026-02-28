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
public sealed class MongoDbConfigurator(IServiceCollection serviceCollection)
{
    /// <summary>
    /// Gets the types of models that have been registered with MongoDB collections.
    /// </summary>
    public IReadOnlyCollection<Type> MongoModelTypes => _mongoModelTypes;
    private readonly List<Type> _mongoModelTypes = [];

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
    public MongoDbConfigurator AddCollection<TModel>(IMongoCollection<TModel> collection)
    {
        _mongoModelTypes.Add(typeof(TModel));
        serviceCollection.AddTransient<IMongoCollectionInternal<TModel>>(_ =>
            new MongoCollectionInternal<TModel>(collection));
        return this;
    }
}