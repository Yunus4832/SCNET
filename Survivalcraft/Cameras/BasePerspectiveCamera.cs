using Engine.Graphics;

namespace Game.Cameras;

public abstract class BasePerspectiveCamera(GameWidget gameWidget) : Camera(gameWidget)
{
    private Matrix? _invertedProjectionMatrix;

    private Matrix? _invertedViewMatrix;

    private Matrix? _projectionMatrix;

    private Matrix? _screenProjectionMatrix;

    private Vector3 _viewDirection;

    private BoundingFrustum? _viewFrustum;

    private bool _viewFrustumValid;

    private Matrix? _viewMatrix;

    private Matrix? _viewportMatrix;

    private Vector2? _viewportSize;

    private Vector3 _viewPosition;

    private Matrix? _viewProjectionMatrix;

    private Vector3 _viewRight;

    private Vector3 _viewUp;

    public override Vector3 ViewPosition => _viewPosition;

    public override Vector3 ViewDirection => _viewDirection;

    public override Vector3 ViewUp => _viewUp;

    public override Vector3 ViewRight => _viewRight;

    public override Matrix ViewMatrix
    {
        get
        {
            if (_viewMatrix.HasValue)
            {
                return _viewMatrix.Value;
            }

            if (!Eye.HasValue)
            {
                _viewMatrix = Matrix.CreateLookAt(_viewPosition, _viewPosition + _viewDirection, _viewUp);
            }
            else
            {
                var eyeToHeadTransform = VrManager.GetEyeToHeadTransform(Eye.Value);
                _viewMatrix = Matrix.CreateLookAt(_viewPosition, _viewPosition + _viewDirection, _viewUp) *
                              Matrix.Invert(eyeToHeadTransform);
            }

            return _viewMatrix.Value;
        }
    }

    public override Matrix InvertedViewMatrix
    {
        get
        {
            if (!_invertedViewMatrix.HasValue)
            {
                _invertedViewMatrix = Matrix.Invert(ViewMatrix);
            }

            return _invertedViewMatrix.Value;
        }
    }

    public override Matrix ProjectionMatrix
    {
        get
        {
            if (!_projectionMatrix.HasValue)
            {
                _projectionMatrix = CalculateBaseProjectionMatrix();
                var viewWidget = GameWidget.ViewWidget;
                if (!viewWidget.ScalingRenderTargetSize.HasValue && !Eye.HasValue)
                {
                    _projectionMatrix *=
                        MatrixUtils.CreateScaleTranslation(0.5f * viewWidget.ActualSize.X,
                            -0.5f * viewWidget.ActualSize.Y, viewWidget.ActualSize.X / 2f,
                            viewWidget.ActualSize.Y / 2f) * viewWidget.GlobalTransform *
                        MatrixUtils.CreateScaleTranslation(2f / Display.Viewport.Width, -2f / Display.Viewport.Height,
                            -1f, 1f);
                }
            }

            return _projectionMatrix.Value;
        }
    }

    public override Matrix ScreenProjectionMatrix
    {
        get
        {
            if (!_screenProjectionMatrix.HasValue)
            {
                if (!Eye.HasValue)
                {
                    var size = Window.Size;
                    var viewWidget = GameWidget.ViewWidget;
                    _screenProjectionMatrix = CalculateBaseProjectionMatrix() *
                                              MatrixUtils.CreateScaleTranslation(0.5f * viewWidget.ActualSize.X,
                                                  -0.5f * viewWidget.ActualSize.Y, viewWidget.ActualSize.X / 2f,
                                                  viewWidget.ActualSize.Y / 2f) * viewWidget.GlobalTransform *
                                              MatrixUtils.CreateScaleTranslation(2f / size.X, -2f / size.Y, -1f, 1f);
                }
                else
                {
                    _screenProjectionMatrix = CalculateBaseProjectionMatrix();
                }
            }

            return _screenProjectionMatrix.Value;
        }
    }

    public override Matrix InvertedProjectionMatrix
    {
        get
        {
            if (!_invertedProjectionMatrix.HasValue)
            {
                _invertedProjectionMatrix = Matrix.Invert(ProjectionMatrix);
            }

            return _invertedProjectionMatrix.Value;
        }
    }

    public override Matrix ViewProjectionMatrix
    {
        get
        {
            if (!_viewProjectionMatrix.HasValue)
            {
                _viewProjectionMatrix = ViewMatrix * ProjectionMatrix;
            }

            return _viewProjectionMatrix.Value;
        }
    }

    public override Vector2 ViewportSize
    {
        get
        {
            if (_viewportSize.HasValue)
            {
                return _viewportSize.Value;
            }

            var viewWidget = GameWidget.ViewWidget;
            if (viewWidget.ScalingRenderTargetSize.HasValue)
            {
                _viewportSize = new Vector2(viewWidget.ScalingRenderTargetSize.Value);
            }
            else
            {
                _viewportSize = !Eye.HasValue
                    ? new Vector2(viewWidget.ActualSize.X * viewWidget.GlobalTransform.Right.Length(),
                        viewWidget.ActualSize.Y * viewWidget.GlobalTransform.Up.Length())
                    : (Vector2?)new Vector2(
                        VrManager.VrRenderTarget?.Width ?? 0,
                        VrManager.VrRenderTarget?.Height ?? 0
                    );
            }

            return _viewportSize.Value;
        }
    }

    public override Matrix ViewportMatrix
    {
        get
        {
            if (_viewportMatrix.HasValue)
            {
                return _viewportMatrix.Value;
            }

            if (!Eye.HasValue)
            {
                var viewWidget = GameWidget.ViewWidget;
                if (viewWidget.ScalingRenderTargetSize.HasValue)
                {
                    _viewportMatrix = Matrix.Identity;
                }
                else
                {
                    var identity = Matrix.Identity;
                    identity.Right = Vector3.Normalize(viewWidget.GlobalTransform.Right);
                    identity.Up = Vector3.Normalize(viewWidget.GlobalTransform.Up);
                    identity.Forward = viewWidget.GlobalTransform.Forward;
                    identity.Translation = viewWidget.GlobalTransform.Translation;
                    _viewportMatrix = identity;
                }
            }
            else
            {
                _viewportMatrix = Matrix.Identity;
            }

            return _viewportMatrix.Value;
        }
    }

    public override BoundingFrustum ViewFrustum
    {
        get
        {
            if (_viewFrustumValid)
            {
                return _viewFrustum!;
            }

            if (_viewFrustum is null)
            {
                _viewFrustum = new BoundingFrustum(ViewProjectionMatrix);
            }
            else
            {
                _viewFrustum.Matrix = ViewProjectionMatrix;
            }

            _viewFrustumValid = true;

            return _viewFrustum!;
        }
    }

    public override void PrepareForDrawing(VrEye? eye)
    {
        base.PrepareForDrawing(eye);
        _viewMatrix = null;
        _invertedViewMatrix = null;
        _projectionMatrix = null;
        _invertedProjectionMatrix = null;
        _screenProjectionMatrix = null;
        _viewProjectionMatrix = null;
        _viewportSize = null;
        _viewportMatrix = null;
        _viewFrustumValid = false;
    }

    protected void SetupPerspectiveCamera(Vector3 position, Vector3 direction, Vector3 up)
    {
        _viewPosition = position;
        _viewDirection = Vector3.Normalize(direction);
        _viewUp = Vector3.Normalize(up);
        _viewRight = Vector3.Normalize(Vector3.Cross(_viewDirection, _viewUp));
    }

    private Matrix CalculateBaseProjectionMatrix()
    {
        if (Eye.HasValue)
        {
            return VrManager.GetProjectionMatrix(Eye.Value, 0.1f, 2048f);
        }

        var num = 80f * SettingsManager.ViewAngle;
        var viewWidget = GameWidget.ViewWidget;
        var num2 = viewWidget.ActualSize.X / viewWidget.ActualSize.Y;
        var num3 = MathUtils.Min(num * num2, num);
        var num4 = num3 * num2;
        if (num4 < 90f)
        {
            num3 *= 90f / num4;
        }
        else if (num4 > 175f)
        {
            num3 *= 175f / num4;
        }

        return Matrix.CreatePerspectiveFieldOfView(MathUtils.DegToRad(num3), num2, 0.1f, 2048f);
    }
}
