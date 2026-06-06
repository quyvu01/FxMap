using FxMap.Abstractions;
using Shared.DistributedKeys;

namespace Service2.Pipelines;

public sealed class TestCustomUseData : ICustomExpressionBehavior<UserDistributedKey>
{
    public string CustomExpression() => "CustomExpression";

    public async Task<Dictionary<string, object>> HandleAsync(RequestContext<UserDistributedKey> requestContext)
    {
        // await Task.Delay(TimeSpan.FromSeconds(0.5));
        await Task.Yield();
        return requestContext.Query.SelectorIds.ToDictionary(kv => kv, object (kv) => $"Hello: {kv}");
    }
}