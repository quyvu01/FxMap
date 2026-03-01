namespace FxMap.Fluent.Rules;

public sealed class ConditionalExpression(
    (Func<IServiceProvider, CancellationToken, Task<bool>> AsyncCondition, string Expression) condition,
    Func<IServiceProvider, CancellationToken, Task<string>> expressionFuncAsync)
{
    public async Task<string> ResolveAsync(IServiceProvider serviceProvider, CancellationToken token = default)
    {
        if (condition.AsyncCondition is { } asyncCondition && await asyncCondition(serviceProvider, token))
            return condition.Expression;
        if (expressionFuncAsync != null)
            return await expressionFuncAsync(serviceProvider, token);
        return null;
    }
}