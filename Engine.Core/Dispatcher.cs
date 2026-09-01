namespace Engine.Core;

public static class Dispatcher
{
    private static int? _mainThreadId;

    private static readonly List<ActionInfo> _actionInfos = [];

    private static readonly List<ActionInfo> _currentActionInfos = [];

    public static int MainThreadId =>
        _mainThreadId ?? throw new InvalidOperationException("Dispatcher is not initialized.");

    public static bool IsMainThread => Environment.CurrentManagedThreadId == MainThreadId;

    public static void Dispatch(Action action, bool waitUntilCompleted = false)
    {
        if (!_mainThreadId.HasValue)
        {
            throw new InvalidOperationException("Dispatcher is not initialized.");
        }

        ActionInfo actionInfo;
        if (_mainThreadId.Value == Environment.CurrentManagedThreadId)
        {
            action();
        }
        else if (waitUntilCompleted)
        {
            actionInfo = default;
            actionInfo.Action = action;
            actionInfo.Event = new ManualResetEventSlim(false);
            var item = actionInfo;
            lock (_actionInfos)
            {
                _actionInfos.Add(item);
            }

            item.Event.Wait();
            item.Event.Dispose();
        }
        else
        {
            lock (_actionInfos)
            {
                var actionInfos = _actionInfos;
                actionInfo = new ActionInfo
                {
                    Action = action
                };
                actionInfos.Add(actionInfo);
            }
        }
    }

    public static void Initialize()
    {
        _mainThreadId = Environment.CurrentManagedThreadId;
    }

    public static void Dispose()
    {
    }

    public static void BeforeFrame()
    {
        _currentActionInfos.Clear();
        lock (_actionInfos)
        {
            _currentActionInfos.AddRange(_actionInfos);
            _actionInfos.Clear();
        }

        foreach (var currentActionInfo in _currentActionInfos)
        {
            try
            {
                currentActionInfo.Action?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error("Dispatched action failed. Reason: {0}", ex);
            }
            finally
            {
                currentActionInfo.Event?.Set();
            }
        }
    }

    public static void AfterFrame()
    {
    }

    private struct ActionInfo
    {
        public Action? Action;

        public ManualResetEventSlim? Event;
    }
}
