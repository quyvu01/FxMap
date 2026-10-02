using System.Linq.Expressions;
using FxMap.Extensions;
using FxMap.Fluent.Rules;

namespace FxMap.Fluent.Builders;

/// <summary>
/// Configures how each row found for a selector value fills one element of a collection property.
/// Created by <see cref="PropertyRuleBuilder{TModel}.Collection{TItem}"/>.
/// </summary>
/// <typeparam name="TItem">The element type of the collection.</typeparam>
public sealed class CollectionRuleBuilder<TItem>
{
    private readonly CollectionRule _rule;

    internal CollectionRuleBuilder(CollectionRule rule) => _rule = rule;

    /// <summary>
    /// Fills a property of each element from an expression evaluated on the row of that element.
    /// Without an expression the default property of the entity is used.
    /// </summary>
    public CollectionRuleBuilder<TItem> For<TProp>(Expression<Func<TItem, TProp>> targetProperty,
        string expression = null)
    {
        _rule.Rules.Add(new PropertyMappingRule
        {
            TargetPropertyName = targetProperty.GetPropertyInfo().Name,
            TargetPropertyInfo = targetProperty.GetPropertyInfo(),
            Expression = expression
        });
        return this;
    }

    /// <summary>Fills a property of each element from an expression chosen at mapping time.</summary>
    public CollectionRuleBuilder<TItem> For<TProp>(Expression<Func<TItem, TProp>> targetProperty,
        Action<ConditionalExpressionBuilder> conditionalBuilder)
    {
        var builder = new ConditionalExpressionBuilder();
        conditionalBuilder(builder);
        _rule.Rules.Add(new PropertyMappingRule
        {
            TargetPropertyName = targetProperty.GetPropertyInfo().Name,
            TargetPropertyInfo = targetProperty.GetPropertyInfo(),
            ConditionalExpression = builder.Build()
        });
        return this;
    }

    /// <summary>
    /// Sorts the rows of each selector value ascending by a property of the entity that owns the data
    /// (not of <typeparamref name="TItem"/>). Replaces any sort configured before.
    /// </summary>
    public CollectionRuleBuilder<TItem> OrderBy(string entityProperty)
    {
        _rule.OrderBy.Clear();
        return ThenBy(entityProperty);
    }

    /// <summary>Same as <see cref="OrderBy"/>, descending. Replaces any sort configured before.</summary>
    public CollectionRuleBuilder<TItem> OrderByDescending(string entityProperty)
    {
        _rule.OrderBy.Clear();
        return ThenByDescending(entityProperty);
    }

    /// <summary>Adds a lower priority ascending sort key.</summary>
    public CollectionRuleBuilder<TItem> ThenBy(string entityProperty) => AddOrder(entityProperty, false);

    /// <summary>Adds a lower priority descending sort key.</summary>
    public CollectionRuleBuilder<TItem> ThenByDescending(string entityProperty) => AddOrder(entityProperty, true);

    /// <summary>Keeps at most <paramref name="count"/> rows (elements) for each selector value.</summary>
    public CollectionRuleBuilder<TItem> Limit(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        _rule.Limit = count;
        return this;
    }

    private CollectionRuleBuilder<TItem> AddOrder(string entityProperty, bool descending)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityProperty);
        _rule.OrderBy.Add(new CollectionOrder(entityProperty, descending));
        return this;
    }
}
