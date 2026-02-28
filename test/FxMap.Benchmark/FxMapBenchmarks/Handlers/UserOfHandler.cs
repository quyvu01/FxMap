using System.Text.Json;
using FxMap.Abstractions;
using FxMap.Benchmark.Attributes;
using FxMap.Responses;

namespace FxMap.Benchmark.FxMapBenchmarks.Handlers;

public sealed class UserOfHandler : IClientRequestHandler<UserOfAttribute>
{
    public Task<ItemsResponse<DataResponse>> RequestAsync(RequestContext<UserOfAttribute> requestContext)
    {
        var query = requestContext.Query;
        var expressions = query.Expressions;
        var result = query.SelectorIds
            .Select(id => new DataResponse
            {
                Id = id, Values =
                [
                    ..expressions.Select(a =>
                    {
                        var valueResult = $"Some value from expression {a}";
                        return new ValueResponse
                        {
                            Expression = a, Value = JsonSerializer.Serialize(valueResult)
                        };
                    })
                ]
            });
        return Task.FromResult(new ItemsResponse<DataResponse>([..result]));
    }
}