namespace Game.Modding;

/// <summary>管理已安装的 CPU 模组环境，并串行执行替换事务。</summary>
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

    /// <summary>所有旧世界释放后预留环境替换权。预留期间旧环境不能接纳新世界，调用方可释放旧环境的客户端资源。Commit 成功前，<paramref name="runtime"/> 仍由调用方持有。</summary>
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

        /// <summary>释放旧 CPU 环境并接管替换环境。</summary>
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
