using System.Linq.Expressions;
using System.Reflection;
using OfX.Extensions;
using OfX.Fluent.Rules;

namespace OfX.Fluent.Builders;

public sealed class PropertyRuleBuilder<TModel>
{
    private readonly KeyRuleGroup _group;

    internal PropertyRuleBuilder(KeyRuleGroup group) => _group = group;

    public PropertyRuleBuilder<TModel> For<TProp>(Expression<Func<TModel, TProp>> targetProperty,
        string expression = null)
    {
        _group.Rules.Add(new PropertyMappingRule
        {
            TargetPropertyName = GetPropertyName(targetProperty),
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
            TargetPropertyName = GetPropertyName(targetProperty),
            TargetPropertyInfo = targetProperty.GetPropertyInfo(),
            ConditionalExpression = builder.Build()
        });
        return this;
    }

    private static string GetPropertyName<TProp>(Expression<Func<TModel, TProp>> expression)
    {
        if (expression.Body is MemberExpression member)
            return member.Member.Name;
        throw new ArgumentException("Expression must be a property accessor.");
    }
}