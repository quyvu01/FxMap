namespace FxMap.Abstractions;

/// <summary>
/// The base interface for all FxMap distributed keys.
/// </summary>
/// <remarks>
/// <para>
/// This is the foundation of the FxMap mapping system. All specific distributed key types
/// (e.g., <c>IUserKey</c>, <c>IOrderKey</c>) should implement this interface.
/// </para>
/// <para>
/// Distributed keys identify which remote service or data provider should be queried
/// for a given property mapping. Property mappings are configured via <c>ProfileOf&lt;T&gt;</c>
/// using the FluentAPI rather than attribute decoration.
/// </para>
/// <example>
/// <code>
/// // Define a distributed key
/// public interface IUserKey : IDistributedKey;
///
/// // Configure mapping in a ProfileOf&lt;T&gt;
/// public class OrderResponseProfile : ProfileOf&lt;OrderResponse&gt;
/// {
///     protected override void Configure()
///     {
///         UseDistributedKey&lt;IUserKey&gt;()
///             .Selector(x => x.UserId)
///             .Map(x => x.UserName, "Name");
///     }
/// }
/// </code>
/// </example>
/// </remarks>
public interface IDistributedKey;