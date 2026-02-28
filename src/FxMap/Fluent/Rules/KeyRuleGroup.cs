namespace FxMap.Fluent.Rules;

public sealed class KeyRuleGroup
{
    public Type DistributedKeyType { get; set; }
    public string DistributedKey { get; set; }
    public string SelectorPropertyName { get; set; }
    public List<PropertyMappingRule> Rules { get; } = [];
}
