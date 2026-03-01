using System.Reflection;
using FxMap.Accessors.PropertyAccessors;
using FxMap.Fluent.Rules;
using FxMap.PropertyMappingContexts;

namespace FxMap.Fluent;

public interface IFluentProfileConfig
{
    Type ModelType { get; }
    IReadOnlyCollection<KeyRuleGroup> RuleGroups { get; }
    IReadOnlyDictionary<PropertyInfo, IPropertyAccessor> Accessors { get; }
    IReadOnlyDictionary<PropertyInfo, PropertyContext[]> DependencyGraphs { get; }
    PropertyInformation GetInformation(PropertyInfo propertyInfo);
    void Build();
}