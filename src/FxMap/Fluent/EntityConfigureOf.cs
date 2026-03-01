using System.Linq.Expressions;
using FxMap.Abstractions;
using FxMap.Exceptions;
using FxMap.Extensions;

namespace FxMap.Fluent;

public abstract class EntityConfigureOf<TModel> : IFluentEntityConfig where TModel : class
{
    protected EntityConfigureOf() => Configure();

    Type IFluentEntityConfig.EntityType => typeof(TModel);
    string IFluentEntityConfig.IdPropertyName => IdPropertyName;
    string IFluentEntityConfig.DefaultPropertyName => DefaultPropertyName;
    IReadOnlyCollection<ExposedNameStore> IFluentEntityConfig.ExposedNameStores => [.._exposedNameStores];
    Type IFluentEntityConfig.DistributedKeyType => DistributedKeyType;
    string IFluentEntityConfig.DistributedKey => DistributedKey;
    private string IdPropertyName { get; set; }
    private readonly List<ExposedNameStore> _exposedNameStores = [];
    private readonly HashSet<string> _exposedPropertyNames = [];
    private string DefaultPropertyName { get; set; }
    private Type DistributedKeyType { get; set; }
    private string DistributedKey { get; set; }

    protected void Id<TProp>(Expression<Func<TModel, TProp>> selector)
        => IdPropertyName = GetPropertyName(selector);

    protected void DefaultProperty<TProp>(Expression<Func<TModel, TProp>> selector)
        => DefaultPropertyName = GetPropertyName(selector);

    protected void ExposedName<TProp>(Expression<Func<TModel, TProp>> selector, string exposedName)
    {
        if (!_exposedPropertyNames.Add(exposedName))
            throw new FxMapException.DuplicatedNameByExposedName(typeof(TModel), exposedName);
        _exposedNameStores.Add(new ExposedNameStore(selector.GetPropertyInfo(), exposedName));
    }

    protected void UseDistributedKey(string distributedKey) => DistributedKey = distributedKey;

    protected void UseDistributedKey<TDistributedKey>() where TDistributedKey : IDistributedKey
        => DistributedKeyType = typeof(TDistributedKey);

    protected abstract void Configure();

    private static string GetPropertyName<TProp>(Expression<Func<TModel, TProp>> expression)
    {
        if (expression.Body is MemberExpression member)
            return member.Member.Name;
        throw new ArgumentException("Expression must be a property accessor.");
    }
}