namespace Game.Automation;

/// <summary>Converges the normal first-person view through player look input.</summary>
public sealed class AutomationViewController
{
    private ComponentPlayer? _player;
    private Vector3 _target;
    private float _tolerance;
    private float _error;
    private double _deadline;
    private long _id;
    private string _status = "idle";
    private string? _reason;

    public object Capture() => new
    {
        Id = _id,
        Status = _status,
        Reason = _reason,
        Target = new { _target.X, _target.Y, _target.Z },
        ErrorDegrees = MathUtils.RadToDeg(_error)
    };

    public long Start(ComponentPlayer player, Vector3 target, float toleranceDegrees, double timeoutSeconds)
    {
        if (!float.IsFinite(target.X) || !float.IsFinite(target.Y) || !float.IsFinite(target.Z) ||
            !float.IsFinite(toleranceDegrees) || toleranceDegrees is < 0.1f or > 5f ||
            !double.IsFinite(timeoutSeconds) || timeoutSeconds is < 1 or > 30)
        {
            throw new ArgumentException("Finite coordinates, toleranceDegrees 0.1–5 and timeoutSeconds 1–30 are required.");
        }

        if (!CanControl(player))
        {
            throw new InvalidOperationException("A ready first-person character and active stable Game screen are required.");
        }

        var delta = target - player.GameWidget.ActiveCamera.ViewPosition;
        if (!float.IsFinite(delta.LengthSquared()) || delta.LengthSquared() < 0.0001f)
        {
            throw new ArgumentException("The target must have a finite nonzero distance from the camera.");
        }

        if (Math.Abs(MathF.Atan2(delta.Y, delta.XZ.Length())) > MathUtils.DegToRad(82f))
        {
            throw new ArgumentException("The target exceeds the normal -82 to 82 degree pitch range.");
        }

        AutomationInputController.CancelAction("view_started");
        player.ComponentInput.Navigation.Cancel("view_started");
        _player = player;
        _target = target;
        _tolerance = MathUtils.DegToRad(toleranceDegrees);
        _deadline = Time.RealTime + timeoutSeconds;
        _status = "turning";
        _reason = null;
        _error = MathF.PI;
        return ++_id;
    }

    public long StartAngles(ComponentPlayer player, float yawDegrees, float pitchDegrees, bool relative,
        float toleranceDegrees, double timeoutSeconds)
    {
        if (!float.IsFinite(yawDegrees) || !float.IsFinite(pitchDegrees))
        {
            throw new ArgumentException("View angles must be finite.");
        }

        var camera = player.GameWidget.ActiveCamera;
        if (relative)
        {
            yawDegrees = yawDegrees % 360f + MathUtils.RadToDeg(MathF.Atan2(camera.ViewDirection.X, camera.ViewDirection.Z));
            pitchDegrees += MathUtils.RadToDeg(MathF.Asin(Math.Clamp(camera.ViewDirection.Y, -1f, 1f)));
        }

        var direction = AutomationViewSteering.Direction(yawDegrees, pitchDegrees);
        return Start(player, camera.ViewPosition + direction * 1000f, toleranceDegrees, timeoutSeconds);
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
    }

    private static bool CanControl(ComponentPlayer player) =>
        Window.IsActive && ScreensManager.GetCurrentScreenName() == "Game" && !ScreensManager.IsAnimating &&
        player.Entity.IsAddedToProject && player.PlayerData.IsReadyForPlaying &&
        player.ComponentHealth.Health > 0 && player.ComponentSleep.SleepFactor == 0 &&
        player.ComponentGui.ModalPanelWidget == null && !DialogsManager.HasDialogs(player.GuiWidget) &&
        player.GameWidget.ActiveCamera is FppCamera;

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

        if (input.Look != Vector2.Zero || input.Move != Vector3.Zero || input.Jump ||
            input.Dig != null || input.Aim != null || input.Interact != null)
        {
            Cancel("manual_input");
            return;
        }

        var camera = player.GameWidget.ActiveCamera;
        var delta = _target - camera.ViewPosition;
        if (delta.LengthSquared() < 0.0001f)
        {
            Finish("failed", "target_at_camera");
            return;
        }

        var direction = Vector3.Normalize(delta);
        _error = MathF.Acos(Math.Clamp(Vector3.Dot(camera.ViewDirection, direction), -1f, 1f));
        if (_error <= _tolerance)
        {
            Finish("aligned", null);
        }
        else
        {
            input.Look = AutomationViewSteering.Calculate(camera.ViewDirection, direction);
            if (SettingsManager.Current.FlipVerticalAxis)
            {
                input.Look *= new Vector2(1, -1);
            }
        }
    }
}
