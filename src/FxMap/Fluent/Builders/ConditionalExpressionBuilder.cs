using FxMap.Fluent.Rules;

namespace FxMap.Fluent.Builders;

public sealed class ConditionalExpressionBuilder
{
    private Func<IServiceProvider, CancellationToken, Task<bool>> _condition;
    private Func<IServiceProvider, CancellationToken, Task<string>> _ifExpression;
    private Func<IServiceProvider, CancellationToken, Task<string>> _elseExpression;

    public IExpressionStep If(bool condition) =>
        If((_, _) => Task.FromResult(condition));

    public IExpressionStep If(Func<IServiceProvider, bool> condition) =>
        If((sp, _) => Task.FromResult(condition(sp)));

    public IExpressionStep If(Func<IServiceProvider, CancellationToken, Task<bool>> condition)
    {
        _condition = condition;
        return new ExpressionStep(this);
    }

    internal ConditionalExpression Build() => new(_condition, _ifExpression, _elseExpression);

    public interface IExpressionStep
    {
        IElseStep Expression(string expression);
        IElseStep Expression(Func<IServiceProvider, string> expressionFunc);
        IElseStep Expression(Func<IServiceProvider, CancellationToken, Task<string>> expressionFuncAsync);
    }

    public interface IElseStep
    {
        void Else(string elseExpression);
        void Else(Func<IServiceProvider, string> elseExpressionFunc);
        void Else(Func<IServiceProvider, CancellationToken, Task<string>> elseExpressionFuncAsync);
    }

    private sealed class ExpressionStep(ConditionalExpressionBuilder builder) : IExpressionStep
    {
        public IElseStep Expression(string expression)
        {
            builder._ifExpression = (_, _) => Task.FromResult(expression);
            return new ElseStep(builder);
        }

        public IElseStep Expression(Func<IServiceProvider, string> expressionFunc)
        {
            builder._ifExpression = (sp, _) => Task.FromResult(expressionFunc(sp));
            return new ElseStep(builder);
        }

        public IElseStep Expression(Func<IServiceProvider, CancellationToken, Task<string>> expressionFuncAsync)
        {
            builder._ifExpression = expressionFuncAsync;
            return new ElseStep(builder);
        }
    }

    private sealed class ElseStep(ConditionalExpressionBuilder builder) : IElseStep
    {
        public void Else(string elseExpression) =>
            builder._elseExpression = (_, _) => Task.FromResult(elseExpression);

        public void Else(Func<IServiceProvider, string> elseExpressionFunc) =>
            builder._elseExpression = (sp, _) => Task.FromResult(elseExpressionFunc(sp));

        public void Else(Func<IServiceProvider, CancellationToken, Task<string>> elseExpressionFuncAsync) =>
            builder._elseExpression = elseExpressionFuncAsync;
    }
}
