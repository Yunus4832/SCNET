namespace Game.Modding;

/// <summary>Owns the installed CPU mod environment and serializes replacement transactions.</summary>
public static class CurrentModRuntime
{
    private static readonly Lock _gate = new();

    private static GameModRuntime? _value;

    private static Replacement? _pending;

    public static GameModRuntime? Value
    {
        get
        {
            lock (_gate) { return _value; }
        }
    }

    public static event Action<GameModRuntime?>? Changed;

    /// <summary>
    /// Reserves replacement after all old worlds have been released. While reserved, the old
    /// environment cannot acquire new worlds. The caller may now release its client resources.
    /// The caller owns <paramref name="runtime"/> until Commit succeeds.
    /// </summary>
    public static Replacement BeginReplacement(GameModRuntime? runtime)
    {
        lock (_gate)
        {
            if (_pending is not null)
            {
                throw new InvalidOperationException("A mod environment replacement is already pending.");
            }

            if (runtime is not null && ReferenceEquals(runtime, _value))
            {
                throw new InvalidOperationException("The replacement environment is already installed.");
            }

            runtime?.EnsureAlive();
            _value?.BeginRetirement();
            var replacement = new Replacement(_value, runtime);
            _pending = replacement;
            return replacement;
        }
    }

    public sealed class Replacement : IDisposable
    {
        private readonly GameModRuntime? _previous;

        private readonly GameModRuntime? _next;

        private bool _completed;

        internal Replacement(GameModRuntime? previous, GameModRuntime? next)
        {
            _previous = previous;
            _next = next;
        }

        /// <summary>Releases the old CPU environment and transfers ownership of the replacement.</summary>
        public void Commit()
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_completed, this);
                _next?.EnsureAlive();
                // Retirement cannot be rolled back after CPU cleanup has started.
                try { _previous?.Dispose(); }
                catch (Exception error) { Log.Error($"Mod environment retirement cleanup failed: {error}"); }

                _value = _next;
                _pending = null;
                _completed = true;
            }

            foreach (var callback in Changed?.GetInvocationList() ?? [])
            {
                try { ((Action<GameModRuntime?>)callback)(_next); }
                catch (Exception error) { Log.Error($"Mod environment replacement observer failed: {error}"); }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_completed) { return; }

                _previous?.CancelRetirement();
                _pending = null;
                _completed = true;
            }
        }
    }
}
