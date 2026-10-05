using System.Linq.Expressions;
using FxMap.Abstractions;
using FxMap.Helpers;

namespace FxMap.Fluent.Rules;

/// <summary>
/// Groups all mapping rules that share a single distributed key source.
/// Created by each <c>UseDistributedKey(...)</c> call within a <c>ProfileOf&lt;T&gt;</c>.
/// </summary>
public sealed class KeyRuleGroup
{
    /// <summary>
    /// Gets or sets the strongly-typed distributed key CLR type.
    /// Mutually exclusive with <see cref="DistributedKey"/>.
    /// </summary>
    public Type DistributedKeyType { get; set; }

    /// <summary>
    /// Gets or sets the string-based distributed key name.
    /// Mutually exclusive with <see cref="DistributedKeyType"/>.
    /// </summary>
    public string DistributedKey { get; set; }

    /// <summary>
    /// Gets or sets the name of the source (selector) property on the DTO whose value
    /// is sent as the distributed key lookup identifier.
    /// </summary>
    public string SelectorPropertyName { get; set; }

    /// <summary>
    /// Gets or sets the selector when the key is an expression over the DTO (<c>Of(x => x.Id + x.Email)</c>) rather than
    /// a single property. Null when <see cref="SelectorPropertyName"/> is used.
    /// </summary>
    public LambdaExpression SelectorExpression { get; set; }

    /// <summary>
    /// Resolves the effective distributed key CLR type for this group, generating a dynamic
    /// type when a string-based key is used.
    /// </summary>
    /// <returns>The resolved <see cref="Type"/> that implements <see cref="IDistributedKey"/>.</returns>
    public Type GetDistributedKeyType() =>
        DistributedKeyTypeFactory.Resolve(DistributedKeyType, DistributedKey);

    /// <summary>
    /// Gets the list of individual property mapping rules (target property + optional expression)
    /// registered for this distributed key group.
    /// </summary>
    public List<PropertyMappingRule> Rules { get; } = [];
}