using OfX.Abstractions;
using OfX.Fluent.Builders;
using OfX.Fluent.Rules;

namespace OfX.Fluent;

public abstract class ProfileOf<TModel> : IFluentProfileConfig
{
    Type IFluentProfileConfig.ModelType => typeof(TModel);
    List<AttributeRuleGroup> IFluentProfileConfig.RuleGroups => RuleGroups;
    void IFluentProfileConfig.Build() => Configure();

    private List<AttributeRuleGroup> RuleGroups { get; } = [];

    protected AttributeRuleBuilder<TModel> UseKey(string key)
    {
        var group = new AttributeRuleGroup { AttributeKey = key };
        RuleGroups.Add(group);
        return new AttributeRuleBuilder<TModel>(group);
    }

    protected AttributeRuleBuilder<TModel> UseAnnotate<TAttribute>() where TAttribute : IDistributedKey
    {
        var group = new AttributeRuleGroup { AttributeType = typeof(TAttribute) };
        RuleGroups.Add(group);
        return new AttributeRuleBuilder<TModel>(group);
    }

    protected abstract void Configure();
}
