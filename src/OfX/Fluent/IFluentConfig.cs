using OfX.Fluent.Rules;

namespace OfX.Fluent;

internal interface IFluentEntityConfig
{
    Type ModelType { get; }
    string IdPropertyName { get; }
    string DefaultPropertyName { get; }
    IReadOnlyCollection<ExposedNameStore> ExposedNameStores { get; }
    Type AttributeType { get; }
    string AttributeKey { get; }
    void Build();
}

internal interface IFluentProfileConfig
{
    Type ModelType { get; }
    List<AttributeRuleGroup> RuleGroups { get; }
    void Build();
}