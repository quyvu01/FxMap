namespace FxMap.Abstractions;

/// <summary>
/// The base attribute class for all FxMap mapping attributes.
/// </summary>
/// <remarks>
/// <para>
/// This is the foundation of the FxMap mapping system. All specific entity attributes
/// (e.g., <c>UserOfAttribute</c>, <c>OrderOfAttribute</c>) should inherit from this class.
/// </para>
/// <para>
/// When applied to a property, it indicates that the property's value should be fetched
/// from a remote service or data provider based on the selector property value.
/// </para>
/// <example>
/// <code>
/// public class OrderResponse
/// {
///     public string UserId { get; set; }
///
///     [UserOf(nameof(UserId), Expression = "Name")]
///     public string UserName { get; set; }
/// }
/// </code>
/// </example>
/// </remarks>
public interface IDistributedKey;