namespace OfX.Fluent.Rules;

internal sealed class ConditionalExpression(
    List<(Func<bool> SyncCondition, Func<Task<bool>> AsyncCondition, string Expression)> conditions,
    string defaultExpression)
{
    public async Task<string> ResolveAsync()
    {
        foreach (var (sync, async, expr) in conditions)
        {
            if (sync is not null && sync()) return expr;
            if (async is not null && await async()) return expr;
        }

        return defaultExpression;
    }
}
