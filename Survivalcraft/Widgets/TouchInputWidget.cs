using Engine.Input;

namespace Game.Widgets;

public class TouchInputWidget : Widget
{
    private float _radius = 30f;

    private int _touchFrameIndex;

    private int? _touchId;

    private TouchInput? _touchInput;

    private Vector2 _touchLastPosition;

    private bool _touchMoved;

    private Vector2 _touchOrigin;

    private Vector2 _touchOriginLimited;

    private double _touchTime;

    public float Radius
    {
        get => _radius;
        set => _radius = MathUtils.Max(value, 1f);
    }

    public TouchInput? TouchInput
    {
        get
        {
            if (IsEnabledGlobal && IsVisibleGlobal)
            {
                return _touchInput;
            }

            return null;
        }
    }

    public override void Update()
    {
        _touchInput = null;
        var frameStartTime = Time.FrameStartTime;
        var frameIndex = Time.FrameIndex;
        foreach (var touchLocation in Input.TouchLocations)
        {
            if (touchLocation.State == TouchLocationState.Pressed)
            {
                if (HitTestGlobal(touchLocation.Position) != this)
                {
                    continue;
                }

                _touchId = touchLocation.Id;
                _touchLastPosition = touchLocation.Position;
                _touchOrigin = touchLocation.Position;
                _touchOriginLimited = touchLocation.Position;
                _touchTime = frameStartTime;
                _touchFrameIndex = frameIndex;
                _touchMoved = false;
            }
            else if (touchLocation.State == TouchLocationState.Moved)
            {
                if (!_touchId.HasValue || touchLocation.Id != _touchId.Value)
                {
                    continue;
                }

                _touchMoved |= Vector2.Distance(touchLocation.Position, _touchOrigin) >
                               SettingsManager.MinimumDragDistance * GlobalScale;
                TouchInput value = default;
                value.InputType = !_touchMoved ? TouchInputType.Hold : TouchInputType.Move;
                value.Duration = (float)(frameStartTime - _touchTime);
                value.DurationFrames = frameIndex - _touchFrameIndex;
                value.Position = touchLocation.Position;
                value.Move = touchLocation.Position - _touchLastPosition;
                value.TotalMove = touchLocation.Position - _touchOrigin;
                value.TotalMoveLimited = touchLocation.Position - _touchOriginLimited;
                if (MathUtils.Abs(value.TotalMoveLimited.X) > _radius)
                {
                    _touchOriginLimited.X = touchLocation.Position.X -
                                            MathUtils.Sign(value.TotalMoveLimited.X) * _radius;
                }

                if (MathUtils.Abs(value.TotalMoveLimited.Y) > _radius)
                {
                    _touchOriginLimited.Y = touchLocation.Position.Y -
                                            MathUtils.Sign(value.TotalMoveLimited.Y) * _radius;
                }

                _touchInput = value;
                _touchLastPosition = touchLocation.Position;
            }
            else if (touchLocation.State == TouchLocationState.Released && _touchId.HasValue &&
                     touchLocation.Id == _touchId.Value)
            {
                if (frameStartTime - _touchTime <= SettingsManager.MinimumHoldDuration &&
                    Vector2.Distance(touchLocation.Position, _touchOrigin) <
                    SettingsManager.MinimumDragDistance * GlobalScale)
                {
                    TouchInput value2 = default;
                    value2.InputType = TouchInputType.Tap;
                    value2.Duration = (float)(frameStartTime - _touchTime);
                    value2.DurationFrames = frameIndex - _touchFrameIndex;
                    value2.Position = touchLocation.Position;
                    _touchInput = value2;
                }

                _touchId = null;
            }
        }
    }
}
