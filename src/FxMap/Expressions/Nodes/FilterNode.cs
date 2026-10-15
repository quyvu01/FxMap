namespace FxMap.Analyzers;

/// <summary>
/// Represents a filter applied to a collection: Orders:where(Status = 'Completed')
/// </summary>
/// <param name="Source">The source expression (usually a property or navigation).</param>
/// <param name="Condition">The filter condition.</param>
internal sealed record FilterNode(ExpressionNode Source, ConditionNode Condition) : ExpressionNode
{
    public override TResult Accept<TResult, TContext>(IExpressionNodeVisitor<TResult, TContext> visitor, TContext context)
        => visitor.VisitFilter(this, context);
}

/// <summary>
/// Base class for condition nodes used in filters.
/// </summary>
internal abstract record ConditionNode : ExpressionNode;
