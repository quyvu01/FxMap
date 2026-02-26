namespace OfX.Fluent.Rules;

internal sealed class AttributeRuleGroup
{
    public Type AttributeType { get; set; }
    public string AttributeKey { get; set; }
    public string SelectorPropertyName { get; set; }
    public List<PropertyMappingRule> Rules { get; } = [];
}
