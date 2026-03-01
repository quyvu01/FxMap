namespace FxMap.Grpc.Exceptions;

/// <summary>
/// Contains exception types specific to the FxMap gRPC transport.
/// </summary>
public static class GrpcExceptions
{
    /// <summary>
    /// Thrown when the server receives a request for a distributed key type that is not registered in this application.
    /// </summary>
    /// <param name="type">The assembly-qualified type name that could not be deserialized.</param>
    public sealed class CannotDeserializeDistributedKeyType(string type)
        : Exception($"The FxMap distributed key seems not a part of this application: {type}!");
}