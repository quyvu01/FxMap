using OfX.Fluent.Rules;

namespace OfX.Fluent.Builders;

public sealed class ConditionalExpressionBuilder
{
    private readonly List<(Func<IServiceProvider, Task<bool>> AsyncCondition, string Expression)> _conditions = [];

    private string _defaultExpression;

    public ConditionalExpressionBuilder If(Func<IServiceProvider, bool> condition, string expression)
    {
        _conditions.Add((sp => Task.FromResult(condition(sp)), expression));
        return this;
    }

    public ConditionalExpressionBuilder IfAsync(Func<IServiceProvider, Task<bool>> condition, string expression)
    {
        _conditions.Add((condition, expression));
        return this;
    }

    public void Else(string defaultExpression) => _defaultExpression = defaultExpression;

    internal ConditionalExpression Build() => new(_conditions, _defaultExpression);
}