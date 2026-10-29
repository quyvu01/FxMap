namespace FxMap.Abstractions;

/// <summary>
/// The foundational interface for all FxMap components that are bound to a specific <see cref="IDistributedKey"/>.
/// </summary>
/// <typeparam name="TDistributedKey">
/// The type of <see cref="IDistributedKey"/> that defines the metadata, mapping rules,
/// or behavior for the implementing component.
/// </typeparam>
/// <remarks>
/// <para>
/// This is the **starting point** of the FxMap framework — everything begins with an <see cref="IDistributedKey"/>.
/// </para>
/// <para>
/// Any service, handler, or component that participates in the FxMap mapping or data pipeline
/// will typically implement this interface to indicate its association with a specific distributed key type.
/// </para>
/// </remarks>
public interface IMapperBase<TDistributedKey> where TDistributedKey : IDistributedKey;