using System.Linq.Expressions;
using OfX.Fluent.Rules;

namespace OfX.Fluent.Builders;

public sealed class DistributedKeyRuleBuilder<TModel>
{
    private readonly KeyRuleGroup _group;

    internal DistributedKeyRuleBuilder(KeyRuleGroup group) => _group = group;

    public PropertyRuleBuilder<TModel> Of<TProp>(Expression<Func<TModel, TProp>> selectorProperty)
    {
        _group.SelectorPropertyName = GetPropertyName(selectorProperty);
        return new PropertyRuleBuilder<TModel>(_group);
    }

    private static string GetPropertyName<TProp>(Expression<Func<TModel, TProp>> expression)
    {
        if (expression.Body is MemberExpression member)
            return member.Member.Name;
        throw new ArgumentException("Expression must be a property accessor.");
    }
}
