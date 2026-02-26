namespace OfX.Fluent.Rules;

internal sealed class PropertyMappingRule
{
    public string TargetPropertyName { get; init; }
    public string Expression { get; init; }
    public ConditionalExpression ConditionalExpression { get; init; }

    public bool IsConditional => ConditionalExpression is not null;
}
