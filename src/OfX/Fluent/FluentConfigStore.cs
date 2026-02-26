using OfX.Fluent.Rules;

namespace OfX.Fluent;

internal static class FluentConfigStore
{
    internal static Dictionary<Type, EntityConfigMetadata> EntityConfigs { get; } = new();
    internal static Dictionary<Type, List<AttributeRuleGroup>> ProfileConfigs { get; } = new();

    internal static void Clear()
    {
        EntityConfigs.Clear();
        ProfileConfigs.Clear();
    }

    internal static Type ResolveAttributeType(AttributeRuleGroup group, IReadOnlyCollection<Type> knownAttributes) =>
        group.AttributeType ?? knownAttributes.FirstOrDefault(t => t.Name == group.AttributeKey);
}

internal sealed record EntityConfigMetadata(
    Type ModelType,
    Type AttributeType,
    string AttributeKey,
    string IdPropertyName,
    string DefaultPropertyName,
    IReadOnlyCollection<ExposedNameStore> ExposedNameStores);