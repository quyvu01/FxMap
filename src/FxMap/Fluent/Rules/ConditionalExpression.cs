namespace FxMap.Fluent.Rules;

public sealed class ConditionalExpression(
    List<(Func<IServiceProvider, Task<bool>> AsyncCondition, string Expression)> conditions,
    string defaultExpression)
{
    public async Task<string> ResolveAsync(IServiceProvider serviceProvider)
    {
        foreach (var (async, expr) in conditions)
            if (async is not null && await async(serviceProvider))
                return expr;

        return defaultExpression;
    }
}