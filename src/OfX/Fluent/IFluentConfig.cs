using System.Reflection;
using OfX.Accessors.PropertyAccessors;
using OfX.Fluent.Rules;
using OfX.PropertyMappingContexts;

namespace OfX.Fluent;

public interface IFluentEntityConfig
{
    Type ModelType { get; }
    string IdPropertyName { get; }
    string DefaultPropertyName { get; }
    IReadOnlyCollection<ExposedNameStore> ExposedNameStores { get; }
    Type DistributedKeyType { get; }
    string DistributedKey { get; }
}

public interface IFluentProfileConfig
{
    Type ModelType { get; }
    List<KeyRuleGroup> RuleGroups { get; }
    public IReadOnlyDictionary<PropertyInfo, IPropertyAccessor> Accessors { get; }
    IReadOnlyDictionary<PropertyInfo, PropertyContext[]> DependencyGraphs { get; }
    PropertyInformation GetInformation(PropertyInfo propertyInfo);
    void Build();
}