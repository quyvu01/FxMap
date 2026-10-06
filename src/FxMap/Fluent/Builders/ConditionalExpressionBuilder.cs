using FxMap.Fluent.Rules;

namespace FxMap.Fluent.Builders;

/// <summary>
/// Chooses the expression of a rule when the mapping runs: <c>If(condition).Expression(...).Else(...)</c>.
/// </summary>
/// <remarks>
/// <para>
/// The condition and the expressions run <b>concurrently</b> with those of the other rules, and may run more than once in
/// the same request (once for each mapped object), on the <see cref="IServiceProvider"/> they receive. FxMap does not
/// serialize them.
/// </para>
/// <para>
/// Services that are not thread-safe must not be used directly: a scoped Entity Framework <c>DbContext</c> resolved from that
/// provider throws when two conditions use it at the same time. Create what you need inside the delegate
/// (<c>IDbContextFactory&lt;T&gt;</c>, or a scope from <c>IServiceScopeFactory</c>), or read cached values only.
/// Keep conditions cheap and free of side effects.
/// </para>
/// <para>
/// With <c>FxMap.HotChocolate</c> the provider is the scope of the field being resolved, so scoped services start empty
/// (state that your own middleware put in the scope of the request is not visible there).
/// </para>
/// </remarks>
public sealed class ConditionalExpressionBuilder
{
    private Func<IServiceProvider, CancellationToken, ValueTask<bool>> _condition;
    private Func<IServiceProvider, CancellationToken, ValueTask<string>> _ifExpression;
    private Func<IServiceProvider, CancellationToken, ValueTask<string>> _elseExpression;

    public IExpressionStep If(Func<IServiceProvider, bool> condition) =>
        If((sp, _) => ValueTask.FromResult(condition(sp)));

    public IExpressionStep If(Func<IServiceProvider, CancellationToken, ValueTask<bool>> condition)
    {
        _condition = condition;
        return new ExpressionStep(this);
    }

    internal ConditionalExpression Build() => new(_condition, _ifExpression, _elseExpression);

    public interface IExpressionStep
    {
        IElseStep Expression(string expression);
        IElseStep Expression(Func<IServiceProvider, string> expressionFunc);
        IElseStep Expression(Func<IServiceProvider, CancellationToken, ValueTask<string>> expressionFuncAsync);
    }

    public interface IElseStep
    {
        void Else(string elseExpression);
        void Else(Func<IServiceProvider, string> elseExpressionFunc);
        void Else(Func<IServiceProvider, CancellationToken, ValueTask<string>> elseExpressionFuncAsync);
    }

    private sealed class ExpressionStep(ConditionalExpressionBuilder builder) : IExpressionStep
    {
        public IElseStep Expression(string expression)
        {
            builder._ifExpression = (_, _) => ValueTask.FromResult(expression);
            return new ElseStep(builder);
        }

        public IElseStep Expression(Func<IServiceProvider, string> expressionFunc)
        {
            builder._ifExpression = (sp, _) => ValueTask.FromResult(expressionFunc(sp));
            return new ElseStep(builder);
        }

        public IElseStep Expression(Func<IServiceProvider, CancellationToken, ValueTask<string>> expressionFuncAsync)
        {
            builder._ifExpression = expressionFuncAsync;
            return new ElseStep(builder);
        }
    }

    private sealed class ElseStep(ConditionalExpressionBuilder builder) : IElseStep
    {
        public void Else(string elseExpression) =>
            builder._elseExpression = (_, _) => ValueTask.FromResult(elseExpression);

        public void Else(Func<IServiceProvider, string> elseExpressionFunc) =>
            builder._elseExpression = (sp, _) => ValueTask.FromResult(elseExpressionFunc(sp));

        public void Else(Func<IServiceProvider, CancellationToken, ValueTask<string>> elseExpressionFuncAsync) =>
            builder._elseExpression = elseExpressionFuncAsync;
    }
}