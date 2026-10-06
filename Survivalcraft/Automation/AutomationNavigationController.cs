namespace Game.Automation;

/// <summary>Reuses terrain path search but drives the local player through its normal input stage.</summary>
public sealed class AutomationNavigationController
{
    private ComponentPlayer? _player;
    private PathfindingResult? _search;
    private Vector3 _destination;
    private Vector3 _startPosition;
    private Vector3 _lastProgressPosition;
    private double _deadline;
    private double _lastProgressTime;
    private double _lastJumpTime;
    private float _range;
    private long _id;
    private string _status = "idle";
    private string? _reason;

    public object Capture() => new
    {
        Id = _id,
        Status = _status,
        Reason = _reason,
        StartPosition = Coordinates(_startPosition),
        Destination = new { _destination.X, _destination.Y, _destination.Z },
        NextWaypoint = _search is { IsCompleted: true, Path.Count: > 0 }
            ? Coordinates(_search.Path.Array[_search.Path.Count - 1])
            : null,
        RemainingWaypoints = _search is { IsCompleted: true } ? _search.Path.Count : 0,
        PositionsChecked = _search is { IsCompleted: true } ? _search.PositionsChecked : 0,
        PathCost = _search is { IsCompleted: true } ? _search.PathCost : 0f
    };

    private static object Coordinates(Vector3 position) => new { position.X, position.Y, position.Z };

    public long Start(Vector3 destination, float range, double timeoutSeconds)
    {
        if (!float.IsFinite(destination.X) || !float.IsFinite(destination.Y) || !float.IsFinite(destination.Z) ||
            !float.IsFinite(range) || range is < 0.5f or > 4f ||
            !double.IsFinite(timeoutSeconds) || timeoutSeconds is < 1 or > 120)
        {
            throw new ArgumentException("A finite destination, range 0.5–4 and timeoutSeconds 1–120 are required.");
        }

        var player = GameManager.Project?.FindSubsystem<SubsystemPlayers>()?.ComponentPlayers
            .FirstOrDefault(candidate => candidate.IsLocallyControlled);
        if (player == null || !CanControl(player) || ScreensManager.IsAnimating)
        {
            throw new InvalidOperationException("A ready, unmounted walking character and an active stable Game screen are required.");
        }

        if (Vector3.Distance(player.ComponentBody.Position, destination) > 128f)
        {
            throw new ArgumentException("Navigate in stages of at most 128 blocks.");
        }

        var terrain = player.Project.FindSubsystem<SubsystemTerrain>(true)!.Terrain;
        if (terrain.GetChunkAtCell(Terrain.ToCell(destination.X), Terrain.ToCell(destination.Z), false) is not
            { MainThreadState: >= TerrainChunkState.InvalidVertices1 })
        {
            throw new InvalidOperationException("The destination terrain is not loaded yet.");
        }

        AutomationInputController.CancelAction("navigation_started");
        player.ComponentInput.ViewControl.Cancel("navigation_started");
        _id++;
        _destination = destination;
        _startPosition = player.ComponentBody.Position;
        _range = range;
        _deadline = Time.RealTime + timeoutSeconds;
        _lastProgressTime = Time.RealTime;
        _lastProgressPosition = player.ComponentBody.Position;
        _lastJumpTime = Time.RealTime;
        _player = player;
        _reason = null;
        _status = "searching";
        _search = new PathfindingResult();
        player.Project.FindSubsystem<SubsystemPathfinding>(true)!.QueuePathSearch(
            _startPosition + new Vector3(0, 0.01f, 0), destination + new Vector3(0, 0.01f, 0),
            range, player.ComponentBody.BoxSize, false, 2000, _search);
        return _id;
    }

    public void Cancel(string reason = "requested")
    {
        if (_player != null)
        {
            Finish("cancelled", reason);
        }
    }

    private void Finish(string status, string? reason)
    {
        _status = status;
        _reason = reason;
        _player = null;
        // A queued worker owns its result until completion; never reuse or clear it here.
    }

    private static bool CanControl(ComponentPlayer player) =>
        Window.IsActive && ScreensManager.GetCurrentScreenName() == "Game" &&
        player.Entity.IsAddedToProject && player.PlayerData.IsReadyForPlaying &&
        player.ComponentHealth.Health > 0f && player.ComponentSleep.SleepFactor == 0f &&
        player.ComponentGui.ModalPanelWidget == null && !DialogsManager.HasDialogs(player.GuiWidget) &&
        player.GameWidget.ActiveCamera.IsEntityControlEnabled &&
        !player.GameWidget.ActiveCamera.UsesMovementControls &&
        !player.ComponentLocomotion.IsCreativeFlyEnabled &&
        player.Entity.FindComponent<ComponentRider>()?.Mount == null;

    public void UpdateSafety()
    {
        if (_player != null && (GameManager.Project != _player.Project || !CanControl(_player)))
        {
            Cancel("control_unavailable");
        }
        else if (_player != null && Time.RealTime >= _deadline)
        {
            Finish("failed", "timeout");
        }
    }

    public void ApplyInput(ComponentPlayer player, ref PlayerInput input)
    {
        if (_player != player)
        {
            return;
        }

        UpdateSafety();
        if (_player == null)
        {
            return;
        }

        if (input.Move != Vector3.Zero || input.Look != Vector2.Zero || input.Jump ||
            input.Dig != null || input.Interact != null || input.Aim != null)
        {
            Cancel("manual_input");
            return;
        }

        var position = player.ComponentBody.Position;
        if (Vector3.Distance(position, _destination) <= _range)
        {
            Finish("arrived", null);
            return;
        }

        if (_search is not { IsCompleted: true })
        {
            return;
        }

        while (_search.Path.Count > 1 && Vector3.Distance(position, _search.Path.Array[_search.Path.Count - 1]) < 0.6f)
        {
            _search.Path.RemoveAt(_search.Path.Count - 1);
        }

        if (_search.Path.Count == 0)
        {
            Finish("failed", "no_path");
            return;
        }

        if (Vector3.DistanceSquared(position, _lastProgressPosition) > 0.16f)
        {
            _lastProgressTime = Time.RealTime;
            _lastProgressPosition = position;
        }
        else if (Time.RealTime - _lastProgressTime > 5)
        {
            Finish("failed", "stuck");
            return;
        }

        _status = "walking";
        var waypoint = _search.Path.Array[_search.Path.Count - 1];
        var target = AutomationNavigationSteering.SelectTarget(waypoint, _destination, _range, _search.Path.Count == 1);
        if (_search.Path.Count == 1 && target == waypoint && Vector3.Distance(position, waypoint) < 0.6f)
        {
            Finish("failed", "partial_path");
            return;
        }

        var delta = target - position;
        var steering = AutomationNavigationSteering.Calculate(position, player.ComponentBody.Matrix.Forward, target);
        input.Look = steering.Look;
        input.Move = steering.Move;
        input.SneakMove = steering.SneakMove;
        if (delta.Y > 0.4f && player.ComponentBody.StandingOnValue != null && Time.RealTime - _lastJumpTime > 0.5)
        {
            input.Jump = true;
            _lastJumpTime = Time.RealTime;
        }
    }
}
