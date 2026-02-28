using System.Reflection;

namespace FxMap.Serializable;

/// <summary>
/// Provides JSON serialization utilities using System.Text.Json for the FxMap framework.
/// </summary>
/// <remarks>
/// This class wraps the native .NET JSON serialization to provide a consistent serialization
/// interface across the framework. All FxMap data is serialized as JSON strings for transport.
/// </remarks>
public static class JsonSerializer
{
    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>A JSON string representation of the object.</returns>
    public static string SerializeObject(object obj) => System.Text.Json.JsonSerializer.Serialize(obj);

    /// <summary>
    /// Deserializes a JSON string to an object of the specified type.
    /// </summary>
    /// <param name="objSerialized">The JSON string to deserialize.</param>
    /// <param name="objectType">The target type for deserialization.</param>
    /// <returns>The deserialized object.</returns>
    public static object DeserializeObject(string objSerialized, Type objectType) =>
        System.Text.Json.JsonSerializer.Deserialize(objSerialized, objectType);


    internal static readonly MethodInfo SerializeObjectMethod = typeof(JsonSerializer)
        .GetMethod(nameof(SerializeObject), [typeof(object)]);
}