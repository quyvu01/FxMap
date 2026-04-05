using System.Linq.Expressions;
using FxMap.Abstractions;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.Helpers;

namespace FxMap.Fluent;

/// <summary>
/// Base class for configuring how a <typeparamref name="TModel"/> entity is exposed
/// to the FxMap distributed mapping engine.
/// </summary>
/// <typeparam name="TModel">The entity type being configured.</typeparam>
public abstract class EntityConfigureOf<TModel> : IFluentEntityConfig where TModel : class
{
    protected EntityConfigureOf() => Configure();

    Type IFluentEntityConfig.EntityType => typeof(TModel);
    string IFluentEntityConfig.IdPropertyName => IdPropertyName;
    string IFluentEntityConfig.DefaultPropertyName => DefaultPropertyName;
    IReadOnlyCollection<ExposedNameStore> IFluentEntityConfig.ExposedNameStores => [.._exposedNameStores];
    Type IFluentEntityConfig.DistributedKeyType => DistributedKeyType;
    string IFluentEntityConfig.DistributedKey => DistributedKey;
    string IFluentEntityConfig.DistributedNamespace => DistributedNamespace;
    private string IdPropertyName { get; set; }
    private readonly List<ExposedNameStore> _exposedNameStores = [];
    private readonly HashSet<string> _exposedPropertyNames = [];
    private string DefaultPropertyName { get; set; }
    private Type DistributedKeyType { get; set; }
    private string DistributedKey { get; set; }
    private string DistributedNamespace { get; set; }

    /// <summary>
    /// Declares which property on <typeparamref name="TModel"/> acts as the primary identifier.
    /// </summary>
    /// <typeparam name="TProp">The type of the identifier property.</typeparam>
    /// <param name="selector">A lambda that selects the identifier property.</param>
    protected void Id<TProp>(Expression<Func<TModel, TProp>> selector)
        => IdPropertyName = GetPropertyName(selector);

    /// <summary>
    /// Declares the default property returned when no explicit expression is specified in a mapping rule.
    /// </summary>
    /// <typeparam name="TProp">The type of the default property.</typeparam>
    /// <param name="selector">A lambda that selects the default property.</param>
    protected void DefaultProperty<TProp>(Expression<Func<TModel, TProp>> selector)
        => DefaultPropertyName = GetPropertyName(selector);

    /// <summary>
    /// Registers an alternative name under which a property is exposed to consuming services.
    /// </summary>
    /// <typeparam name="TProp">The type of the property being aliased.</typeparam>
    /// <param name="selector">A lambda that selects the property.</param>
    /// <param name="exposedName">The alias that consumers will use to reference this property.</param>
    /// <exception cref="DistributedMapException.DuplicatedNameByExposedName">
    /// Thrown when <paramref name="exposedName"/> has already been registered for this entity.
    /// </exception>
    protected void ExposedName<TProp>(Expression<Func<TModel, TProp>> selector, string exposedName)
    {
        if (!_exposedPropertyNames.Add(exposedName))
            throw new DistributedMapException.DuplicatedNameByExposedName(typeof(TModel), exposedName);
        _exposedNameStores.Add(new ExposedNameStore(selector.GetPropertyInfo(), exposedName));
    }

    /// <summary>
    /// Associates this entity with a string-based distributed key, enabling full service decoupling
    /// without a shared key type reference.
    /// </summary>
    /// <param name="distributedKey">
    /// The distributed key name. Must start with a letter or underscore and contain only
    /// letters, digits, or underscores (e.g., <c>"UserKey"</c>).
    /// </param>
    /// <param name="namespace">
    /// The namespace used to scope the dynamic key type (e.g., <c>"MyApp.Keys"</c>).
    /// </param>
    /// <exception cref="DistributedMapException.DistributedKeyNullOrEmpty">
    /// Thrown when <paramref name="distributedKey"/> is <c>null</c> or whitespace.
    /// </exception>
    /// <exception cref="DistributedMapException.InvalidDistributedKeyName">
    /// Thrown when <paramref name="distributedKey"/> does not match the valid identifier pattern.
    /// </exception>
    /// <exception cref="DistributedMapException.DistributedNamespaceNullOrEmpty">
    /// Thrown when <paramref name="namespace"/> is <c>null</c> or whitespace.
    /// </exception>
    /// <exception cref="DistributedMapException.InvalidDistributedNamespace">
    /// Thrown when <paramref name="namespace"/> is not a valid dot-separated identifier.
    /// </exception>
    protected void UseDistributedKey(string distributedKey, string @namespace)
    {
        if (string.IsNullOrWhiteSpace(distributedKey))
            throw new DistributedMapException.DistributedKeyNullOrEmpty();
        DistributedKeyTypeFactory.ValidateKeyName(distributedKey);
        if (string.IsNullOrWhiteSpace(@namespace))
            throw new DistributedMapException.DistributedNamespaceNullOrEmpty();
        DistributedKeyTypeFactory.ValidateNamespace(@namespace);
        (DistributedKey, DistributedNamespace) = (distributedKey, @namespace);
    }

    /// <summary>
    /// Associates this entity with a strongly-typed distributed key, providing compile-time safety
    /// and shared-type coupling between services.
    /// </summary>
    /// <typeparam name="TDistributedKey">
    /// The <see cref="IDistributedKey"/> implementation that identifies this entity.
    /// </typeparam>
    protected void UseDistributedKey<TDistributedKey>() where TDistributedKey : IDistributedKey
        => DistributedKeyType = typeof(TDistributedKey);

    /// <summary>
    /// Override this method to declare the entity's identifier, default property,
    /// distributed key, and any exposed name aliases.
    /// </summary>
    protected abstract void Configure();

    /// <summary>
    /// Extracts the property name from a member-access lambda expression.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="expression">A lambda of the form <c>x => x.Property</c>.</param>
    /// <returns>The name of the selected property.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="expression"/> is not a simple property accessor.
    /// </exception>
    private static string GetPropertyName<TProp>(Expression<Func<TModel, TProp>> expression)
    {
        if (expression.Body is MemberExpression member)
            return member.Member.Name;
        throw new ArgumentException("Expression must be a property accessor.");
    }
}