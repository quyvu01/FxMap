namespace FxMap.Abstractions;

/// <summary>
/// Converts selector IDs into the ID type of the target model at runtime.
/// </summary>
/// <typeparam name="TId">
/// The target type to which the selector IDs should be converted
/// (e.g., <see cref="Guid"/>, <see cref="int"/>, or a custom strongly-typed ID).
/// </typeparam>
/// <remarks>
/// This interface is used by FxMap to make sure that incoming selector IDs (received as <see cref="string"/> values)
/// are converted into their proper type before they are used in a query. Implement it for type-specific ID
/// conversion logic, so that FxMap can work with strongly-typed identifiers.
/// <para>
/// <typeparamref name="TId"/> is invariant (not <c>out</c>) because <see cref="ConvertIds"/> returns a
/// <see cref="List{T}"/>, which is invariant itself.
/// </para>
/// </remarks>
public interface IIdConverter<TId>
{
    /// <summary>
    /// Converts the given selector IDs into the ID type of the target model.
    /// </summary>
    /// <param name="selectorIds">The selector IDs as strings.</param>
    /// <returns>
    /// The converted IDs, ready to be used in queries or lookups. Texts that cannot be converted are left out.
    /// </returns>
    List<TId> ConvertIds(string[] selectorIds);
}
