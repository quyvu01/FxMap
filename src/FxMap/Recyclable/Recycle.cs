namespace FxLink.Wrappers;

/// <summary>
/// Lazily creates a <typeparamref name="T"/> and transparently replaces it with a fresh one once it
/// reports <see cref="IRecyclable.Stopping"/> — callers always read <see cref="Current"/> and never
/// have to know whether the instance they get back is the original or a recreated one.
/// Same pattern as MassTransit's Util.Recycle&lt;T&gt;, minus the full Agent/Supervisor machinery.
/// </summary>
public sealed class Recycle<T> where T : class, IRecyclable
{
    private readonly Func<T> _factory;
    private Lazy<T> _current;

    public Recycle(Func<T> factory)
    {
        _factory = factory;
        CreateNext();
    }

    /// <summary>The current live instance, created on first access. Never a stopped instance.</summary>
    public T Current => Volatile.Read(ref _current).Value;

    private void CreateNext()
    {
        Volatile.Write(ref _current, new Lazy<T>(() =>
        {
            var instance = _factory.Invoke();

            // Only hook Stopping once the instance actually exists — mirrors MassTransit's
            // Recycle<T>, which registers on supervisor.Stopping right after creating it.
            _ = instance.Stopping.ContinueWith(_ => CreateNext(),
                TaskContinuationOptions.RunContinuationsAsynchronously);

            return instance;
        }));
    }
}
