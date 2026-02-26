using System.Linq.Expressions;
using OfX.Abstractions;
using OfX.Exceptions;
using OfX.Extensions;

namespace OfX.Fluent;

public abstract class AbstractOfXConfig<TModel> : IFluentEntityConfig where TModel : class
{
    Type IFluentEntityConfig.ModelType => typeof(TModel);
    string IFluentEntityConfig.IdPropertyName => IdPropertyName;
    string IFluentEntityConfig.DefaultPropertyName => DefaultPropertyName;
    IReadOnlyCollection<ExposedNameStore> IFluentEntityConfig.ExposedNameStores => [.._exposedNameStores];
    Type IFluentEntityConfig.AttributeType => AttributeType;
    string IFluentEntityConfig.AttributeKey => AttributeKey;
    void IFluentEntityConfig.Build() => Configure();

    private string IdPropertyName { get; set; }
    private readonly List<ExposedNameStore> _exposedNameStores = [];
    private readonly HashSet<string> _exposedPropertyNames = [];
    private string DefaultPropertyName { get; set; }
    private Type AttributeType { get; set; }
    private string AttributeKey { get; set; }

    protected void Id<TProp>(Expression<Func<TModel, TProp>> selector)
        => IdPropertyName = GetPropertyName(selector);

    protected void DefaultProperty<TProp>(Expression<Func<TModel, TProp>> selector)
        => DefaultPropertyName = GetPropertyName(selector);

    protected void ExposedName<TProp>(Expression<Func<TModel, TProp>> selector, string exposedName)
    {
        if (!_exposedPropertyNames.Add(exposedName))
            throw new OfXException.DuplicatedNameByExposedName(typeof(TModel), exposedName);
        _exposedNameStores.Add(new ExposedNameStore(selector.GetPropertyInfo(), exposedName));
    }

    protected void UseKey(string key) => AttributeKey = key;

    protected void UseAnnotate<TAttribute>() where TAttribute : IDistributedKey
        => AttributeType = typeof(TAttribute);

    protected abstract void Configure();

    private static string GetPropertyName<TProp>(Expression<Func<TModel, TProp>> expression)
    {
        if (expression.Body is MemberExpression member)
            return member.Member.Name;
        throw new ArgumentException("Expression must be a property accessor.");
    }
}