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
    protected EntityConfigureOf()
    {
        Configure();
        if (IdPropertySelector is null) throw new DistributedMapException.EntityIdNotConfigured(typeof(TModel));
    }

    Type IFluentEntityConfig.EntityType => typeof(TModel);
    // string IFluentEntityConfig.IdPropertyName => IdPropertyName;
    public LambdaExpression IdPropertySelector { get; private set; }
    // string IFluentEntityConfig.DefaultPropertyName => DefaultPropertyName;
    public LambdaExpression DefaultPropertyNameSelector { get; private set; }
    IReadOnlyCollection<ExposedNameStore> IFluentEntityConfig.ExposedNameStores => [.. _exposedNameStores];
    Type IFluentEntityConfig.DistributedKeyType => DistributedKeyType;
    string IFluentEntityConfig.DistributedKey => DistributedKey;
    // private string IdPropertyName { get; set; }
    private readonly List<ExposedNameStore> _exposedNameStores = [];
    private readonly HashSet<string> _exposedPropertyNames = [];
    // private string DefaultPropertyName { get; set; }
    private Type DistributedKeyType { get; set; }
    private string DistributedKey { get; set; }

    /// <summary>
    /// Declares which property on <typeparamref name="TModel"/> acts as the primary identifier.
    /// </summary>
    /// <typeparam name="TProp">The type of the identifier property.</typeparam>
    /// <param name="selector">A lambda that selects the identifier property.</param>
    /// <exception cref="DistributedMapException.InvalidEntitySelector">
    /// Thrown when the selector does not read the entity, or the identifier is declared more than once.
    /// </exception>
    protected void Id<TProp>(Expression<Func<TModel, TProp>> selector)
    {
        ValidateSelector(selector, "Id", IdPropertySelector);
        IdPropertySelector = selector;
    }

    /// <summary>
    /// Declares the default property returned when no explicit expression is specified in a mapping rule.
    /// </summary>
    /// <typeparam name="TProp">The type of the default property.</typeparam>
    /// <param name="selector">A lambda that selects the default property.</param>
    /// <exception cref="DistributedMapException.InvalidEntitySelector">
    /// Thrown when the selector does not read the entity, or the default property is declared more than once.
    /// </exception>
    protected void DefaultProperty<TProp>(Expression<Func<TModel, TProp>> selector)
    {
        ValidateSelector(selector, "DefaultProperty", DefaultPropertyNameSelector);
        DefaultPropertyNameSelector = selector;
    }

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
    /// <exception cref="DistributedMapException.DistributedKeyNullOrEmpty">
    /// Thrown when <paramref name="distributedKey"/> is <c>null</c> or whitespace.
    /// </exception>
    /// <exception cref="DistributedMapException.InvalidDistributedKeyName">
    /// Thrown when <paramref name="distributedKey"/> does not match the valid identifier pattern.
    /// </exception>
    protected void UseDistributedKey(string distributedKey)
    {
        if (string.IsNullOrWhiteSpace(distributedKey))
            throw new DistributedMapException.DistributedKeyNullOrEmpty();
        DistributedKeyTypeFactory.ValidateKeyName(distributedKey);
        DistributedKey = distributedKey;
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
    /// The one place where the Id and DefaultProperty selectors are checked: the rest of FxMap relies on them.
    /// A selector is any lambda over the entity (a property, or a computed value such as <c>x => x.Code + x.Site</c>),
    /// declared once.
    /// </summary>
    private static void ValidateSelector(LambdaExpression selector, string name, LambdaExpression alreadyDeclared)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (alreadyDeclared is not null)
            throw new DistributedMapException.InvalidEntitySelector(typeof(TModel), name, "it is declared more than once.");
        if (!new ParameterUsage(selector.Parameters[0]).IsUsedIn(selector.Body))
            throw new DistributedMapException.InvalidEntitySelector(typeof(TModel), name,
                $"the selector does not read the entity ('{selector}'). Use a property or an expression over it, such as x => x.Code.");
    }

    private sealed class ParameterUsage(ParameterExpression parameter) : ExpressionVisitor
    {
        private bool _used;

        public bool IsUsedIn(Expression body)
        {
            Visit(body);
            return _used;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == parameter) _used = true;
            return node;
        }
    }
}
