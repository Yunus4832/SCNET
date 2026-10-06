namespace Game.Automation;

/// <summary>Tracks one bounded gesture and its terminal state without owning engine input.</summary>
public sealed class AutomationActionScheduler
{
    private long _nextId;
    private int _elapsedFrames;
    private double _elapsedSeconds;

    public long Id { get; private set; }
    public AutomationInputAction? Action { get; private set; }
    public string Status { get; private set; } = "idle";
    public string? CancellationReason { get; private set; }
    public int ElapsedFrames => _elapsedFrames;
    public double ElapsedSeconds => _elapsedSeconds;

    public long Start(AutomationInputAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.DurationFrames is < 1 or > 600 || action.Keys == null || action.MouseButtons == null ||
            action.Keys.Any(key => !Enum.IsDefined(key)) ||
            action.MouseButtons.Any(button => !Enum.IsDefined(button)) ||
            Math.Abs((long)action.MouseDeltaX) > 10000 || Math.Abs((long)action.MouseDeltaY) > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        if (action.DurationSeconds is { } seconds && (!double.IsFinite(seconds) || seconds is <= 0 or > 15))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        if (Action != null)
        {
            throw new InvalidOperationException("Cancel the active gesture before starting another.");
        }

        Id = ++_nextId;
        Action = action with { Keys = action.Keys.Distinct().ToArray(), MouseButtons = action.MouseButtons.Distinct().ToArray() };
        _elapsedFrames = 0;
        _elapsedSeconds = 0;
        Status = "running";
        CancellationReason = null;
        return Id;
    }

    public bool Advance(double elapsedSeconds = 0)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        if (Action == null)
        {
            return false;
        }

        _elapsedFrames++;
        _elapsedSeconds += elapsedSeconds;
        if (Action.DurationSeconds is { } seconds ? _elapsedSeconds < seconds : _elapsedFrames < Action.DurationFrames)
        {
            return true;
        }

        Action = null;
        Status = "completed";
        return false;
    }

    public void Cancel(string reason = "requested")
    {
        if (Action != null)
        {
            Action = null;
            Status = "cancelled";
            CancellationReason = reason;
        }
    }
}
