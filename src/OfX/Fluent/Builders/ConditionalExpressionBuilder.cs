using OfX.Fluent.Rules;

namespace OfX.Fluent.Builders;

public sealed class ConditionalExpressionBuilder
{
    private readonly List<(Func<bool> SyncCondition, Func<Task<bool>> AsyncCondition, string Expression)> _conditions =
        [];

    private string _defaultExpression;

    public ConditionalExpressionBuilder When(Func<bool> condition, string expression)
    {
        _conditions.Add((condition, null, expression));
        return this;
    }

    public ConditionalExpressionBuilder WhenAsync(Func<Task<bool>> condition, string expression)
    {
        _conditions.Add((null, condition, expression));
        return this;
    }

    public void OrElse(string defaultExpression) => _defaultExpression = defaultExpression;

    internal ConditionalExpression Build() => new(_conditions, _defaultExpression);
}
