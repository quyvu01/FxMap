using System.Reflection;
using FxMap.Exceptions;
using FxMap.Fluent.Rules;

namespace FxMap.Fluent;

/// <summary>
/// Rejects <c>Collection(...)</c> rules that cannot work, when the profile is built, so a mistake fails at startup
/// where it was made instead of silently producing an empty or wrong collection while mapping.
/// </summary>
internal static class CollectionRuleValidator
{
    internal static void Validate(Type modelType, IReadOnlyCollection<KeyRuleGroup> groups)
    {
        var modelProperties = modelType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var collections = groups.SelectMany(g => g.Collections.Select(c => (Group: g, Collection: c))).ToList();
        if (collections.Count == 0) return;

        var valueRuleTargets = groups.SelectMany(g => g.Rules).Select(r => r.TargetPropertyName).ToHashSet();
        var selectors = groups.Select(g => g.SelectorPropertyName).Where(n => n is not null).ToHashSet();
        var seen = new HashSet<string>();

        foreach (var (group, collection) in collections)
        {
            var name = collection.TargetPropertyName;
            Fail(Exists(modelProperties, group.SelectorPropertyName) is false,
                modelType, name, $"its selector '{group.SelectorPropertyName}' is not a public property of the model.");
            Fail(!seen.Add(name), modelType, name,
                "it is the target of more than one Collection rule. Use one Collection rule per property.");
            Fail(valueRuleTargets.Contains(name), modelType, name,
                "it is also the target of a For(...) rule. A property is either filled with one value or with a collection.");
            Fail(selectors.Contains(name), modelType, name,
                "it is also used as the selector of a key (Of(...)). A collection cannot identify a row.");
            Fail(collection.TargetPropertyInfo.SetMethod is not { IsPublic: true }, modelType, name,
                "it has no public setter, so the mapper cannot assign the elements to it.");
            Fail(collection.Rules.Count == 0, modelType, name,
                $"it has no For(...) rule for {collection.ItemType.Name}, so there is nothing to fill in each element.");

            var itemTargets = new HashSet<string>();
            foreach (var rule in collection.Rules)
            {
                Fail(!itemTargets.Add(rule.TargetPropertyName), modelType, name,
                    $"{collection.ItemType.Name}.{rule.TargetPropertyName} is the target of more than one For(...) rule.");
                Fail(rule.TargetPropertyInfo.SetMethod is not { IsPublic: true }, modelType, name,
                    $"{collection.ItemType.Name}.{rule.TargetPropertyName} has no public setter.");
            }
        }
    }

    // null when there is no selector name to check (the caller treats that as "nothing to report here")
    private static bool? Exists(PropertyInfo[] properties, string name) =>
        name is null ? null : properties.Any(p => p.Name == name);

    private static void Fail(bool condition, Type modelType, string propertyName, string reason)
    {
        if (condition) throw new DistributedMapException.InvalidCollectionRule(modelType, propertyName, reason);
    }
}
