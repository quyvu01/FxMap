namespace FxMap.Fluent.Rules;

public sealed class ConditionalExpression(
    Func<IServiceProvider, CancellationToken, Task<bool>> condition,
    Func<IServiceProvider, CancellationToken, Task<string>> ifExpression,
    Func<IServiceProvider, CancellationToken, Task<string>> elseExpression)
{
    public async Task<string> ResolveAsync(IServiceProvider serviceProvider, CancellationToken token = default)
    {
        if (condition is not null && await condition(serviceProvider, token))
            return ifExpression is not null ? await ifExpression(serviceProvider, token) : null;
        return elseExpression is not null ? await elseExpression(serviceProvider, token) : null;
    }
}
