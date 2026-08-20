namespace FxLink.Wrappers;

/// <summary>
/// A resource with a lifetime that can end on its own (broker-closed channel, dropped connection, ...).
/// <see cref="Recycle{T}"/> uses <see cref="Stopping"/> to know when to transparently replace it.
/// </summary>
public interface IRecyclable
{
    /// <summary>Completes once this instance should no longer be used and a replacement is needed.</summary>
    Task Stopping { get; }
}
