using System.Reflection;
using FxMap.Accessors.PropertyAccessors;

namespace FxMap.Models;

/// <summary>
/// One property of a profile, with everything the mapper needs while walking an object, resolved once.
/// </summary>
/// <param name="Property">The property.</param>
/// <param name="Accessor">The compiled accessor of the property.</param>
/// <param name="Information">Mapping information of the property (shared, never mutated).</param>
/// <param name="IsRule">True when the property is the target of a mapping rule.</param>
internal sealed record ProfileEntry(
    PropertyInfo Property,
    IPropertyAccessor Accessor,
    PropertyInformation Information,
    bool IsRule);

/// <summary>
/// Immutable, per-profile description of what the walker has to do for an object of that profile:
/// rule properties produce descriptors, other properties are walked into only when their value can hold objects.
/// </summary>
internal sealed class ProfilePlan(ProfileEntry[] ruleEntries, ProfileEntry[] walkEntries)
{
    /// <summary>Properties that are the target of a mapping rule.</summary>
    internal ProfileEntry[] RuleEntries { get; } = ruleEntries;

    /// <summary>Properties without a rule whose declared type can hold an object that needs walking.</summary>
    internal ProfileEntry[] WalkEntries { get; } = walkEntries;
}

/// <summary>Implemented by profiles able to hand out their precomputed <see cref="ProfilePlan"/>.</summary>
internal interface IProfilePlanSource
{
    ProfilePlan Plan { get; }
}
