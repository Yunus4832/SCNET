using Engine.Core;

using Window = Engine.Windowing.Window;

namespace Engine.Input;

public static partial class Touch
{
    private static readonly List<TouchLocation> _touchLocations = [];

    public static ReadOnlyList<TouchLocation> TouchLocations => new(_touchLocations);

    public static event Action<TouchLocation>? TouchPressed;

    public static event Action<TouchLocation>? TouchReleased;

    public static event Action<TouchLocation>? TouchMoved;

    public static bool IsTouched;

    internal static void Initialize()
    {
    }

    internal static void Dispose()
    {
    }

    public static void Clear() => _touchLocations.Clear();

    internal static void BeforeFrame()
    {
        BeforeFramePlatform();
    }

    internal static void AfterFrame()
    {
        for (var i = 0; i < _touchLocations.Count; i++)
        {
            if (_touchLocations[i].State == TouchLocationState.Released)
            {
                _touchLocations.RemoveAt(i);
                continue;
            }

            if (_touchLocations[i].releaseQueued)
            {
                _touchLocations[i] = new TouchLocation
                {
                    Id = _touchLocations[i].Id,
                    Position = _touchLocations[i].Position,
                    State = TouchLocationState.Released
                };
            }
            else if (_touchLocations[i].State == TouchLocationState.Pressed)
            {
                _touchLocations[i] = new TouchLocation
                {
                    Id = _touchLocations[i].Id,
                    Position = _touchLocations[i].Position,
                    State = TouchLocationState.Moved
                };
            }
        }
    }

    static int FindTouchLocationIndex(int id)
    {
        for (var i = 0; i < _touchLocations.Count; i++)
        {
            if (_touchLocations[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    public static void ProcessTouchPressed(int id, Vector2 position) => ProcessTouchMoved(id, position);

    public static void ProcessTouchMoved(int id, Vector2 position)
    {
        if (!Window.IsActive)
        {
            return;
        }

        IsTouched = true;
        var num = FindTouchLocationIndex(id);
        if (num >= 0)
        {
            if (_touchLocations[num].State == TouchLocationState.Moved)
            {
                _touchLocations[num] = new TouchLocation
                    { Id = id, Position = position, State = TouchLocationState.Moved };
            }

            TouchMoved?.Invoke(_touchLocations[num]);
        }
        else
        {
            _touchLocations.Add(new TouchLocation
                { Id = id, Position = position, State = TouchLocationState.Pressed });
            TouchPressed?.Invoke(_touchLocations[^1]);
        }
    }

    public static void ProcessTouchReleased(int id, Vector2 position)
    {
        if (!Window.IsActive)
        {
            return;
        }

        var num = FindTouchLocationIndex(id);
        if (num < 0)
        {
            return;
        }

        if (_touchLocations[num].State == TouchLocationState.Pressed)
        {
            _touchLocations[num] = new TouchLocation
            {
                Id = id,
                Position = position,
                State = TouchLocationState.Pressed,
                releaseQueued = true
            };
        }
        else
        {
            _touchLocations[num] = new TouchLocation
                { Id = id, Position = position, State = TouchLocationState.Released };
        }

        TouchReleased?.Invoke(_touchLocations[num]);
    }

    static partial void BeforeFramePlatform();
}
