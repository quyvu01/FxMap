using System.Linq.Expressions;
using System.Reflection;
using FxMap.Exceptions;
using FxMap.Fluent.Rules;
using FxMap.Helpers;

namespace FxMap.Fluent.Builders;

public sealed class DistributedKeyRuleBuilder<TModel>
{
    private readonly KeyRuleGroup _group;

    internal DistributedKeyRuleBuilder(KeyRuleGroup group) => _group = group;

    /// <summary>
    /// Declares where the key of the rules comes from: a property of the DTO (<c>Of(x => x.UserId)</c>), or any
    /// expression over it (<c>Of(x => x.Mrn + x.Code)</c>), for an entity whose <c>Id(...)</c> is computed the same way.
    /// The value is read as text with <c>ToString()</c>. An expression that goes through a null has no key.
    /// </summary>
    /// <param name="selectorProperty">The property or the expression that gives the key.</param>
    /// <exception cref="DistributedMapException.InvalidProfileSelector">The selector does not read the DTO.</exception>
    public PropertyRuleBuilder<TModel> Of<TProp>(Expression<Func<TModel, TProp>> selectorProperty)
    {
        ArgumentNullException.ThrowIfNull(selectorProperty);
        var body = selectorProperty.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert)
            body = convert.Operand;

        if (body is MemberExpression { Member: PropertyInfo property } member &&
            member.Expression == selectorProperty.Parameters[0])
        {
            _group.SelectorPropertyName = property.Name;
            _group.SelectorExpression = null;
        }
        else
        {
            if (!SelectorExpressions.ReadsParameter(selectorProperty))
                throw new DistributedMapException.InvalidProfileSelector(typeof(TModel),
                    $"the selector does not read the model ('{selectorProperty}'). Use a property or an expression over it, such as x => x.UserId.");
            _group.SelectorPropertyName = null;
            _group.SelectorExpression = selectorProperty;
        }

        return new PropertyRuleBuilder<TModel>(_group);
    }
}
