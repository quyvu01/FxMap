using MongoDB.Driver;

namespace FxMap.MongoDb.Abstractions;

/// <summary>
/// Internal interface for wrapping MongoDB collections in the FxMap MongoDB integration.
/// </summary>
/// <typeparam name="TCollection">The document type of the collection.</typeparam>
internal interface IMongoCollectionInternal<TCollection>
{
    /// <summary>
    /// Gets the underlying MongoDB collection.
    /// </summary>
    IMongoCollection<TCollection> Collection { get; }
}