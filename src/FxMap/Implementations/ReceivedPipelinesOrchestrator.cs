using System.Text.Json;
using FxMap.Abstractions;
using FxMap.Models;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.Responses;

namespace FxMap.Implementations;

/// <summary>
/// Abstract base class for server-side pipeline orchestration in the FxMap framework.
/// </summary>
/// <remarks>
/// This class provides the abstract contract for executing received requests on the server side.
/// Concrete implementations handle the deserialization, pipeline execution, and response building.
/// </remarks>
public abstract class ReceivedPipelinesOrchestrator
{
    /// <summary>
    /// Executes the received request and returns the data response.
    /// </summary>
    /// <param name="message">The FxMap request containing selector IDs and expressions.</param>
    /// <param name="headers">Request headers that may contain context information.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The items response containing the fetched data.</returns>
    public abstract Task<ItemsResponse<DataResponse>> ExecuteAsync(FxMapRequest message,
        Dictionary<string, string> headers, CancellationToken cancellationToken);
}

/// <summary>
/// Server-side pipeline orchestrator that executes received pipeline behaviors and query handlers.
/// </summary>
/// <typeparam name="TModel">The entity model type being queried.</typeparam>
/// <typeparam name="TDistributedKey">The FxMap attribute type associated with this handler.</typeparam>
/// <param name="behaviors">The collection of received pipeline behaviors to execute.</param>
/// <param name="handlers">The query handlers that fetch data from the data source.</param>
/// <param name="customExpressionHandlers">Handlers for custom expression evaluation.</param>
/// <remarks>
/// This orchestrator:
/// <list type="bullet">
///   <item><description>Deserializes incoming expressions from the request</description></item>
///   <item><description>Separates custom expressions from standard expressions</description></item>
///   <item><description>Executes pipeline behaviors in reverse order (middleware pattern)</description></item>
///   <item><description>Merges results from custom expression handlers with standard results</description></item>
/// </list>
/// </remarks>
public class ReceivedPipelinesOrchestrator<TModel, TDistributedKey>(
    IEnumerable<IReceivedPipelineBehavior<TDistributedKey>> behaviors,
    IEnumerable<IQueryOfHandler<TModel, TDistributedKey>> handlers,
    IEnumerable<ICustomExpressionBehavior<TDistributedKey>> customExpressionHandlers) :
    ReceivedPipelinesOrchestrator,
    IReceivedPipelinesOrchestrator<TDistributedKey>
    where TDistributedKey : IDistributedKey where TModel : class
{
    public async Task<ItemsResponse<DataResponse>> ExecuteAsync(RequestContext<TDistributedKey> requestContext)
    {
        var executableHandlers = handlers
            .Where(x => x is not NoOpQueryOfHandler)
            .ToArray();
        var handler = executableHandlers.Length switch
        {
            0 => throw new FxMapException.CannotFindHandlerForOfAttribute(typeof(TDistributedKey)),
            1 => executableHandlers.First(),
            _ => throw new FxMapException.AttributeHasBeenConfiguredForModel(typeof(TModel), typeof(TDistributedKey)),
        };

        // Deserialize expressions from Expression, we handle the custom expressions and original expression as well
        var expressions = requestContext.Query.Expressions;
        var customExpressions = customExpressionHandlers
            .Select(a => a.CustomExpression())
            .ToArray();
        var newExpressions = expressions.Except(customExpressions).ToArray();
        var customExpressionsToExecute = customExpressions.Intersect(expressions);

        var newRequestContext = new RequestContextImpl<TDistributedKey>(
            requestContext.Query with { Expressions = newExpressions }, requestContext.Headers,
            requestContext.CancellationToken);

        var resultTask = behaviors.Reverse()
            .Aggregate(() => handler.GetDataAsync(newRequestContext),
                (acc, pipeline) => () => pipeline.HandleAsync(newRequestContext, acc)).Invoke();

        if (newExpressions.Length == expressions.Length) return await resultTask;

        // Handle getting data for custom expressions
        var customResults = customExpressionHandlers
            .Where(a => customExpressionsToExecute.Contains(a.CustomExpression()))
            .Select(a => (Expression: a.CustomExpression(), ResultTask: a.HandleAsync(
                new RequestContextImpl<TDistributedKey>(requestContext.Query with { Expressions = [a.CustomExpression()] },
                    requestContext.Headers, requestContext.CancellationToken)))).ToList();

        await Task.WhenAll([resultTask, ..customResults.Select(a => a.ResultTask)]);
        var result = await resultTask;

        var customResultsMerged = customResults
            .Select(a => a.ResultTask.Result
                .Select(k => (k.Key, k.Value, a.Expression)))
            .SelectMany(a => a)
            .GroupBy(x => x.Key);

        // Merge custom results with original results
        result.Items.ForEach(it =>
        {
            var customResult = customResultsMerged
                .FirstOrDefault(a => a.Key == it.Id);
            if (customResult is null) return;
            var customValues = customResult.Select(k => new ValueResponse
                { Expression = k.Expression, Value = JsonSerializer.Serialize(k.Value) });
            it.Values = [..it.Values, ..customValues];
        });
        return result;
    }

    public override Task<ItemsResponse<DataResponse>> ExecuteAsync(FxMapRequest message,
        Dictionary<string, string> headers, CancellationToken cancellationToken)
    {
        var requestOf = new MapRequest<TDistributedKey>(message.SelectorIds, message.Expressions);
        var requestContext = new RequestContextImpl<TDistributedKey>(requestOf, headers ?? [], cancellationToken);
        return ExecuteAsync(requestContext);
    }
}