using MongoDB.Driver;
using FxMap.MongoDb.Abstractions;

namespace FxMap.MongoDb.Implementations;

/// <summary>
/// Internal implementation of <see cref="IMongoCollectionInternal{TCollection}"/> that wraps a MongoDB collection.
/// </summary>
/// <typeparam name="TCollection">The document type of the collection.</typeparam>
/// <param name="collection">The MongoDB collection to wrap.</param>
internal class MongoCollectionInternal<TCollection>(IMongoCollection<TCollection> collection)
    : IMongoCollectionInternal<TCollection>
{
    /// <inheritdoc />
    public IMongoCollection<TCollection> Collection { get; } = collection;
}