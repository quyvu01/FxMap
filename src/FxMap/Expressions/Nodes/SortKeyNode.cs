namespace FxMap.Analyzers;

/// <summary>
/// A key of <c>:orderBy(...)</c>: a property and its direction. <c>CreatedAt</c> and <c>CreatedAt asc</c> are ascending.
/// </summary>
/// <remarks>
/// It exists only as an argument of the orderBy function, which reads it directly; it is not visited like other nodes.
/// </remarks>
internal sealed record SortKeyNode(string Name, OrderDirection Direction = OrderDirection.Asc) : ExpressionNode
{
    public override TResult Accept<TResult, TContext>(IExpressionNodeVisitor<TResult, TContext> visitor, TContext context)
        => throw new InvalidOperationException("A sort key is only valid as an argument of the orderBy function");
}
