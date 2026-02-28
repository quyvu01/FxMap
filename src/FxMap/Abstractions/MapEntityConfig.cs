namespace FxMap.Abstractions;

/// <summary>
/// Defines the configuration metadata that associates an <see cref="IDistributedKey"/>
/// with a specific model and its key properties.
/// </summary>
/// <remarks>
/// Implementations of this interface specify which property on the model is treated as the identifier,
/// and which property is treated as the "default" value when you don't define the Expression.
/// </remarks>
public interface MapEntityConfig
{
    /// <summary>
    /// Gets the name of the property on the model that represents the unique identifier.
    /// </summary>
    string IdProperty { get; }

    /// <summary>
    /// Gets the name of the property on the model that should be treated as the default property,
    /// typically used for display or default mapping purposes.
    /// </summary>
    string DefaultProperty { get; }
}

/// <summary>
/// Internal record for storing FxMap configuration metadata.
/// </summary>
/// <param name="IdProperty">The name of the ID property.</param>
/// <param name="DefaultProperty">The name of the default property.</param>
internal sealed record FxMapEntityConfig(string IdProperty, string DefaultProperty) : MapEntityConfig;