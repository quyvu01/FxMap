using System.Linq.Expressions;
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
}