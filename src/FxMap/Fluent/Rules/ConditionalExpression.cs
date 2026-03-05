namespace FxMap.Fluent.Rules;

public sealed class ConditionalExpression(
    Func<IServiceProvider, CancellationToken, ValueTask<bool>> condition,
    Func<IServiceProvider, CancellationToken, ValueTask<string>> ifExpression,
    Func<IServiceProvider, CancellationToken, ValueTask<string>> elseExpression)
{
    public async ValueTask<string> ResolveAsync(IServiceProvider serviceProvider, CancellationToken token = default)
    {
        if (condition is not null && await condition(serviceProvider, token))
            return ifExpression is not null ? await ifExpression(serviceProvider, token) : null;
        return elseExpression is not null ? await elseExpression(serviceProvider, token) : null;
    }
}
