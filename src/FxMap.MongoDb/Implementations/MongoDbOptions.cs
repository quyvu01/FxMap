namespace FxMap.MongoDb.Implementations;

/// <summary>The settings of the MongoDB data provider, as given to <c>AddMongoDb</c>.</summary>
internal sealed record MongoDbOptions(bool IsolateExpressionErrors);
