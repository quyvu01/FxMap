using System.Reflection;

namespace FxMap.Fluent.Rules;

public sealed class PropertyMappingRule
{
    public string TargetPropertyName { get; init; }
    public string Expression { get; init; }
    public PropertyInfo TargetPropertyInfo { get; set; }
    public ConditionalExpression ConditionalExpression { get; init; }

    public bool IsConditional => ConditionalExpression is not null;
}