using System.Linq.Expressions;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.Fluent.Rules;

namespace FxMap.Fluent.Builders;

public sealed class PropertyRuleBuilder<TModel>
{
    private readonly KeyRuleGroup _group;

    internal PropertyRuleBuilder(KeyRuleGroup group) => _group = group;

    public PropertyRuleBuilder<TModel> For<TProp>(Expression<Func<TModel, TProp>> targetProperty,
        string expression = null)
    {
        _group.Rules.Add(new PropertyMappingRule
        {
            TargetPropertyName = targetProperty.GetPropertyInfo().Name,
            TargetPropertyInfo = targetProperty.GetPropertyInfo(),
            Expression = expression
        });
        return this;
    }

    public PropertyRuleBuilder<TModel> For<TProp>(Expression<Func<TModel, TProp>> targetProperty,
        Action<ConditionalExpressionBuilder> conditionalBuilder)
    {
        var builder = new ConditionalExpressionBuilder();
        conditionalBuilder(builder);
        _group.Rules.Add(new PropertyMappingRule
        {
            TargetPropertyName = targetProperty.GetPropertyInfo().Name,
            TargetPropertyInfo = targetProperty.GetPropertyInfo(),
            ConditionalExpression = builder.Build()
        });
        return this;
    }

    /// <summary>
    /// Maps every row found for the selector to one element of a collection property, for keys that match several
    /// rows (for example all the visits of one MRN). <paramref name="configure"/> says how each element is filled;
    /// its rules target properties of <typeparamref name="TItem"/>, not of the model.
    /// </summary>
    /// <param name="targetProperty">The collection property of the model, such as <c>x => x.Visits</c>.</param>
    /// <param name="configure">Rules, sort and limit of the elements.</param>
    /// <returns>This builder, so the rules of the model itself can follow.</returns>
    /// <example>
    /// <code>
    /// UseDistributedKey&lt;VisitKey&gt;()
    ///     .Of(x => x.Mrn)
    ///     .Collection(x => x.Visits, v => v
    ///         .For(x => x.Date, "VisitDate")
    ///         .For(x => x.Status)
    ///         .OrderByDescending("VisitDate")
    ///         .Limit(10))
    ///     .For(x => x.PatientName, "Name");
    /// </code>
    /// </example>
    public PropertyRuleBuilder<TModel> Collection<TItem>(Expression<Func<TModel, IEnumerable<TItem>>> targetProperty,
        Action<CollectionRuleBuilder<TItem>> configure) where TItem : class, new()
    {
        ArgumentNullException.ThrowIfNull(configure);
        var property = targetProperty.GetPropertyInfo();
        if (!CanHold(property.PropertyType, typeof(TItem)))
            throw new DistributedMapException.CollectionPropertyTypeNotSupported(typeof(TModel), property.Name,
                property.PropertyType, typeof(TItem));

        var rule = new CollectionRule { TargetPropertyInfo = property, ItemType = typeof(TItem) };
        configure(new CollectionRuleBuilder<TItem>(rule));
        _group.Collections.Add(rule);
        return this;
    }

    // The mapper builds a List<TItem> (or an array); the property must accept one of them.
    private static bool CanHold(Type propertyType, Type itemType) =>
        propertyType == itemType.MakeArrayType() || propertyType.IsAssignableFrom(typeof(List<>).MakeGenericType(itemType));
}