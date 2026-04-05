namespace FxMap.Fluent;

/// <summary>
/// Represents the compiled configuration for a single entity exposed by the FxMap engine.
/// Implementations are produced by <see cref="EntityConfigureOf{TModel}"/>.
/// </summary>
public interface IFluentEntityConfig
{
    /// <summary>Gets the CLR type of the configured entity.</summary>
    Type EntityType { get; }

    /// <summary>Gets the name of the property that acts as the entity's primary identifier.</summary>
    string IdPropertyName { get; }

    /// <summary>
    /// Gets the name of the property returned when no explicit expression is specified in a mapping rule.
    /// </summary>
    string DefaultPropertyName { get; }

    /// <summary>
    /// Gets the collection of exposed-name aliases registered via
    /// <c>ExposedName(selector, alias)</c>.
    /// </summary>
    IReadOnlyCollection<ExposedNameStore> ExposedNameStores { get; }

    /// <summary>
    /// Gets the strongly-typed <see cref="IDistributedKey"/> CLR type, or <c>null</c>
    /// when the entity uses a string-based distributed key instead.
    /// </summary>
    Type DistributedKeyType { get; }

    /// <summary>
    /// Gets the string-based distributed key name, or <c>null</c> when the entity uses
    /// a strongly-typed distributed key instead.
    /// </summary>
    string DistributedKey { get; }

    /// <summary>
    /// Gets the namespace used to scope the dynamic key type generated for a string-based
    /// distributed key. Returns <c>null</c> when a strongly-typed key is used.
    /// </summary>
    string DistributedNamespace { get; }
}