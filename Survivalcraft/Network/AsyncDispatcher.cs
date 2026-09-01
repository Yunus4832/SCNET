using System.Collections.Concurrent;

namespace Game.Network;

public static class AsyncDispatcher
{
    private static readonly ConcurrentQueue<ITask> _actions = new();

    private static readonly List<ITask> _toAdd = [];

    public static void Update()
    {
        if (ScreensManager.CurrentScreen == null)
        {
            return;
        }

        while (_actions.TryDequeue(out var tsk))
        {
            try
            {
                if (!tsk.Update())
                {
                    _toAdd.Add(tsk);
                }
            }
            catch
            {
                // ignored
            }
        }

        foreach (var tsk in _toAdd)
        {
            Dispatch(tsk);
        }

        _toAdd.Clear();
    }

    public static void Dispatch(Action action)
    {
        Dispatch(new SimpleTask(action));
    }

    public static void Dispatch(Func<IEnumerator<bool>> func)
    {
        Dispatch(func());
    }

    public static void Dispatch(IEnumerator<bool> action)
    {
        Dispatch(new Task(action));
    }

    public static void Dispatch(Func<bool> func)
    {
        Dispatch(new RepeatTask(func));
    }

    public static void Dispatch(ITask task)
    {
        _actions.Enqueue(task);
    }

    public interface ITask
    {
        bool Update();
    }

    public class WaitUntil(Func<bool> predicate, Action action) : ITask
    {
        public bool Update()
        {
            if (!predicate())
            {
                return false;
            }

            action.Invoke();
            return true;
        }
    }

    public class SimpleTask(Action action) : ITask
    {
        public bool Update()
        {
            action.Invoke();
            return true;
        }
    }

    public class RepeatTask(Func<bool> func) : ITask
    {
        public bool Update()
        {
            return func();
        }
    }

    public class Task(IEnumerator<bool> generator) : ITask
    {
        private IEnumerator<bool> _generator = generator;

        private Task? _next;

        public bool Update()
        {
            if (_generator.MoveNext() && !_generator.Current)
            {
                return false;
            }

            if (_next != null)
            {
                _generator = _next._generator;
                _next = _next._next;
            }
            else
            {
                return true;
            }

            return false;
        }

        public Task ContinueWith(IEnumerator<bool> generator)
        {
            _next = new Task(generator);
            return _next;
        }
    }
}
