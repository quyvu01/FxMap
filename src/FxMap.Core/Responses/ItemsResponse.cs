using System.Text.Json.Serialization;

namespace FxMap.Responses;

/// <summary>
/// The rows an owner answers with, and the expressions it could not build (see <see cref="ExpressionError"/>).
/// </summary>
public sealed class ItemsResponse<T> where T : class
{
    /// <summary>A response without expression errors.</summary>
    public ItemsResponse(T[] items) : this(items, null)
    {
    }

    [JsonConstructor]
    public ItemsResponse(T[] items, ExpressionError[] errors)
    {
        Items = items;
        Errors = errors ?? [];
    }

    public T[] Items { get; }

    /// <summary>
    /// The expressions of the request that were not built, which is not the case unless the data provider of the owner
    /// isolates expression errors (<c>IsolateExpressionErrors()</c> in <c>AddEntityFrameworkCore</c> or <c>AddMongoDb</c>). The values of these expressions are not in <see cref="Items"/>.
    /// </summary>
    public ExpressionError[] Errors { get; }
}
