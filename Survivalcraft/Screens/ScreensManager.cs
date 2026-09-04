using Engine.Graphics;

namespace Game.Screens;

public static class ScreensManager
{
    public static Dictionary<string, Screen> Screens = new();

    private static AnimationData? _animationData;

    private static readonly PrimitivesRenderer2D _pr2 = new();

    private static readonly PrimitivesRenderer3D _pr3 = new();

    private static readonly Random _sharedRandom = new(0);

    private static RenderTarget2D? _uiRenderTarget;

    private static Vector3 _vrQuadPosition;

    private static Matrix _vrQuadMatrix;

    private const float _debugUiScale = 1f;

    public static ContainerWidget RootWidget
    {
        get => field is not null ? field : throw new InvalidOperationException("RootWidget is not initialized");
        set;
    } = null!;

    public static bool IsAnimating => _animationData != null;

    public static Screen? CurrentScreen { get; set; }

    public static Screen? PreviousScreen { get; set; }

    public static event Action? OnEnterScreen;

    public static T? FindScreen<T>(string name, bool throwIfNull = false) where T : Screen
    {
        Screens.TryGetValue(name, out var value);
        if (value is null && throwIfNull)
        {
            throw new InvalidOperationException("Screen not found");
        }

        return (T?)value;
    }

    public static T? FindScreen<T>() where T : Screen
    {
        return Screens.Values.Where(s => s.GetType() == typeof(T)).Cast<T>().FirstOrDefault();
    }

    public static void AddScreen(string name, Screen screen)
    {
        Screens.Add(name, screen);
    }

    public static string GetCurrentScreenName()
    {
        return CurrentScreen != null ? GetScreenName(CurrentScreen) : string.Empty;
    }

    public static void SwitchScreen(string? name, params object[] parameters)
    {
        SwitchScreen(string.IsNullOrEmpty(name) ? null : FindScreen<Screen>(name), parameters);
    }

    public static void SwitchScreen(Screen? screen, params object[] parameters)
    {
        if (screen == CurrentScreen)
        {
            return;
        }

        if (_animationData != null)
        {
            EndAnimation();
        }

        _animationData = new AnimationData
        {
            NewScreen = screen,
            OldScreen = CurrentScreen,
            Parameters = parameters,
            Speed = CurrentScreen == null ? float.MaxValue : 4f
        };
        if (CurrentScreen != null)
        {
            RootWidget.IsUpdateEnabled = false;
            CurrentScreen.Input.Clear();
        }

        PreviousScreen = CurrentScreen!;
        CurrentScreen = screen;
        UpdateAnimation();
        if (CurrentScreen != null)
        {
            Log.Verbose($"Entered screen \"{GetScreenName(CurrentScreen)}\"");
        }
    }

    public static void Initialize()
    {
        RootWidget = new CanvasWidget();
        RootWidget.WidgetsHierarchyInput = new WidgetInput();
        InitScreens();
        SwitchScreen("Loading");
    }

    private static void InitScreens()
    {
        var loadingScreen = new LoadingScreen();
        AddScreen("Loading", loadingScreen);
    }


    public static void Update()
    {
        if (_animationData != null)
        {
            UpdateAnimation();
        }

        Widget.UpdateWidgetsHierarchy(RootWidget);
    }

    public static void Draw()
    {
        Utilities.Dispose(ref _uiRenderTarget);
        LayoutAndDrawWidgets();
    }

    private static void UpdateAnimation()
    {
        var num = MathUtils.Min(Time.FrameDuration, 0.1f);
        var factor = _animationData!.Factor;
        _animationData.Factor = MathUtils.Min(_animationData.Factor + _animationData.Speed * num, 1f);
        if (_animationData.Factor < 0.5f)
        {
            if (_animationData.OldScreen != null)
            {
                var num2 = 2f * (0.5f - _animationData.Factor);
                var scale = 1f;
                _animationData.OldScreen.ColorTransform = new Color(num2, num2, num2, num2);
                _animationData.OldScreen.RenderTransform =
                    Matrix.CreateTranslation((0f - _animationData.OldScreen.ActualSize.X) / 2f,
                        (0f - _animationData.OldScreen.ActualSize.Y) / 2f, 0f) * Matrix.CreateScale(scale) *
                    Matrix.CreateTranslation(_animationData.OldScreen.ActualSize.X / 2f,
                        _animationData.OldScreen.ActualSize.Y / 2f, 0f);
            }
        }
        else if (factor < 0.5f)
        {
            if (_animationData.OldScreen != null)
            {
                _animationData.OldScreen.Leave();
                RootWidget.Children.Remove(_animationData.OldScreen);
            }

            if (_animationData.NewScreen != null)
            {
                RootWidget.Children.Insert(0, _animationData.NewScreen);
                _animationData.NewScreen.Enter(_animationData.Parameters);
                OnEnterScreen?.Invoke();
                _animationData.NewScreen.ColorTransform = Color.Transparent;
                RootWidget.IsUpdateEnabled = true;
            }
        }
        else if (_animationData.NewScreen != null)
        {
            var num3 = 2f * (_animationData.Factor - 0.5f);
            var scale2 = 1f;
            _animationData.NewScreen.ColorTransform = new Color(num3, num3, num3, num3);
            _animationData.NewScreen.RenderTransform =
                Matrix.CreateTranslation((0f - _animationData.NewScreen.ActualSize.X) / 2f,
                    (0f - _animationData.NewScreen.ActualSize.Y) / 2f, 0f) * Matrix.CreateScale(scale2) *
                Matrix.CreateTranslation(_animationData.NewScreen.ActualSize.X / 2f,
                    _animationData.NewScreen.ActualSize.Y / 2f, 0f);
        }

        if (_animationData.Factor >= 1f)
        {
            EndAnimation();
        }
    }

    private static void EndAnimation()
    {
        if (_animationData!.NewScreen != null)
        {
            _animationData.NewScreen.ColorTransform = Color.White;
            _animationData.NewScreen.RenderTransform = Matrix.CreateScale(1f);
        }

        _animationData = null;
    }

    private static string GetScreenName(Screen screen)
    {
        var key = Screens.FirstOrDefault(kvp => kvp.Value == screen).Key;
        return key ?? string.Empty;
    }

    public static void AnimateVrQuad()
    {
        if (Time.FrameIndex < 5 || _uiRenderTarget is null)
        {
            return;
        }

        const float num = 6f;
        var hmdMatrix = Matrix.Identity;
        var vector = hmdMatrix.Translation + num * (Vector3.Normalize(hmdMatrix.Forward * new Vector3(1f, 0f, 1f)) +
                                                    new Vector3(0f, 0.1f, 0f));
        if (_vrQuadPosition == Vector3.Zero)
        {
            _vrQuadPosition = vector;
        }

        if (Vector3.Distance(_vrQuadPosition, vector) > 0f)
        {
            var v = vector * new Vector3(1f, 0f, 1f) - _vrQuadPosition * new Vector3(1f, 0f, 1f);
            var v2 = vector * new Vector3(0f, 1f, 0f) - _vrQuadPosition * new Vector3(0f, 1f, 0f);
            var num2 = v.Length();
            var num3 = v2.Length();
            _vrQuadPosition +=
                v * MathUtils.Min(
                    0.75f * MathUtils.Pow(MathUtils.Max(num2 - 0.15f * num, 0f), 0.33f) * Time.FrameDuration, 1f);
            _vrQuadPosition +=
                v2 * MathUtils.Min(
                    1.5f * MathUtils.Pow(MathUtils.Max(num3 - 0.05f * num, 0f), 0.33f) * Time.FrameDuration, 1f);
        }

        var vector2 = new Vector2(_uiRenderTarget.Width / (float)_uiRenderTarget.Height, 1f);
        vector2 /= MathUtils.Max(vector2.X, vector2.Y);
        vector2 *= 7.5f;
        _vrQuadMatrix.Forward = Vector3.Normalize(hmdMatrix.Translation - _vrQuadPosition);
        _vrQuadMatrix.Right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, _vrQuadMatrix.Forward)) * vector2.X;
        _vrQuadMatrix.Up = Vector3.Normalize(Vector3.Cross(_vrQuadMatrix.Forward, _vrQuadMatrix.Right)) *
                           vector2.Y;
        _vrQuadMatrix.Translation = _vrQuadPosition - 0.5f * (_vrQuadMatrix.Right + _vrQuadMatrix.Up);
        RootWidget.WidgetsHierarchyInput!.VrQuadMatrix = _vrQuadMatrix;
    }

    public static void DrawVrQuad()
    {
        if (_uiRenderTarget is null)
        {
            return;
        }

        QueueQuad(
            _pr3.TexturedBatch(_uiRenderTarget, false, 0, DepthStencilState.Default, RasterizerState.CullNoneScissor,
                BlendState.Opaque, SamplerState.LinearClamp), _vrQuadMatrix.Translation, _vrQuadMatrix.Right,
            _vrQuadMatrix.Up, Color.White);
    }

    public static void DrawVrBackground()
    {
        var hmdMatrix = Matrix.Identity;
        var batch = _pr3.TexturedBatch(ContentManager.Get<Texture2D>("Textures/Star"));
        _sharedRandom.Seed(0);
        for (var i = 0; i < 1500; i++)
        {
            var f = MathUtils.Pow(_sharedRandom.Float(0f, 1f), 6f);
            var rgb = (MathUtils.Lerp(0.05f, 0.4f, f) * Color.White).RGB;
            const int num = 6;
            var vector = _sharedRandom.Vector3(500f);
            var vector2 = Vector3.Normalize(Vector3.Cross(vector, Vector3.UnitY)) * num;
            var up = Vector3.Normalize(Vector3.Cross(vector2, vector)) * num;
            QueueQuad(batch, vector + hmdMatrix.Translation, vector2, up, rgb);
        }

        var batch2 = _pr3.TexturedBatch(ContentManager.Get<Texture2D>("Textures/Blocks"), true, 1, null, null, null,
            SamplerState.PointClamp);
        for (var j = -8; j <= 8; j++)
        {
            for (var k = -8; k <= 8; k++)
            {
                var num2 = 1f;
                var num3 = 1f;
                var vector3 = new Vector3((j - 0.5f) * num2, 0f, (k - 0.5f) * num2) +
                              new Vector3(MathUtils.Round(hmdMatrix.Translation.X), 0f,
                                  MathUtils.Round(hmdMatrix.Translation.Z));
                var num4 = Vector3.Distance(vector3, hmdMatrix.Translation);
                var num5 = MathUtils.Lerp(1f, 0f, MathUtils.Saturate(num4 / 7f));
                if (num5 > 0f)
                {
                    QueueQuad(batch2, vector3, new Vector3(num3, 0f, 0f), new Vector3(0f, 0f, num3), Color.Gray * num5,
                        new Vector2(0.1875f, 0.25f), new Vector2(0.25f, 0.3125f));
                }
            }
        }
    }

    private static void LayoutAndDrawWidgets()
    {
        if (_animationData != null)
        {
            Display.Clear(Color.Black, 1f, 0);
        }

        var num = 850f / MathUtils.Clamp(SettingsManager.Current.UIScale, 0.5f, 1.2f) * _debugUiScale;
        var vector = new Vector2(Display.Viewport.Width, Display.Viewport.Height);
        var num2 = vector.X / num;
        var availableSize = new Vector2(num, num / vector.X * vector.Y);
        var num3 = num * 9f / 16f;
        if (vector.Y / num2 < num3)
        {
            num2 = vector.Y / num3;
            availableSize = new Vector2(num3 / vector.Y * vector.X, num3);
        }

        RootWidget.LayoutTransform = Matrix.CreateScale(num2, num2, 1f);
        if (SettingsManager.Current.UpsideDownLayout)
        {
            RootWidget.LayoutTransform *= new Matrix(-1f, 0f, 0f, 0f, 0f, -1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f);
        }

        Widget.LayoutWidgetsHierarchy(RootWidget, availableSize);
        Widget.DrawWidgetsHierarchy(RootWidget);
    }

    public static void QueueQuad(FlatBatch3D batch, Vector3 corner, Vector3 right, Vector3 up, Color color)
    {
        var p = corner + right;
        var p2 = corner + right + up;
        var p3 = corner + up;
        batch.QueueQuad(corner, p, p2, p3, color);
    }

    private static void QueueQuad(TexturedBatch3D batch, Vector3 center, Vector3 right, Vector3 up, Color color)
    {
        QueueQuad(batch, center, right, up, color, new Vector2(0f, 0f), new Vector2(1f, 1f));
    }

    private static void QueueQuad(TexturedBatch3D batch, Vector3 corner, Vector3 right, Vector3 up, Color color,
        Vector2 tc1, Vector2 tc2)
    {
        var p = corner + right;
        var p2 = corner + right + up;
        var p3 = corner + up;
        batch.QueueQuad(corner, p, p2, p3, new Vector2(tc1.X, tc2.Y), new Vector2(tc2.X, tc2.Y),
            new Vector2(tc2.X, tc1.Y), new Vector2(tc1.X, tc1.Y), color);
    }

    private class AnimationData
    {
        public float Factor;

        public Screen? NewScreen;

        public Screen? OldScreen;

        public object[] Parameters = [];

        public float Speed;
    }
}
