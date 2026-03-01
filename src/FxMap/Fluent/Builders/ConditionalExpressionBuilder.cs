using FxMap.Fluent.Rules;

namespace FxMap.Fluent.Builders;

public sealed class ConditionalExpressionBuilder
{
    private (Func<IServiceProvider, CancellationToken, Task<bool>> AsyncCondition, string Expression)
        _condition;

    private Func<IServiceProvider, CancellationToken, Task<string>> _expressionFuncAsync;

    public ConditionalExpressionBuilder If(bool condition, string expression)
    {
        _condition = ((sp, _) => Task.FromResult(condition), expression);
        return this;
    }

    public ConditionalExpressionBuilder If(Func<IServiceProvider, bool> condition, string expression)
    {
        _condition = ((sp, _) => Task.FromResult(condition(sp)), expression);
        return this;
    }

    public ConditionalExpressionBuilder If(Func<IServiceProvider, CancellationToken, Task<bool>> condition,
        string expression)
    {
        _condition = (condition, expression);
        return this;
    }

    public void Else(string elseExpression) => _expressionFuncAsync = (_, _) => Task.FromResult(elseExpression);

    public void Else(Func<IServiceProvider, string> elseExpressionFunc)
    {
        _expressionFuncAsync = (sp, _) => Task.FromResult(elseExpressionFunc(sp));
    }

    public void Else(Func<IServiceProvider, CancellationToken, Task<string>> expressionFuncAsync)
    {
        _expressionFuncAsync = expressionFuncAsync;
    }


    internal ConditionalExpression Build() => new(_condition, _expressionFuncAsync);
}