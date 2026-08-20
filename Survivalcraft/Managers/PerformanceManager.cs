using Engine.Graphics;
using Engine.Media;

using Game.Network;
using Game.Network.Enums;

namespace Game.Managers;

public static class PerformanceManager
{
    private static readonly PrimitivesRenderer2D _primitivesRenderer;

    private static readonly RunningAverage _averageFrameTime;

    private static float _cpuUtilSumNumerator;

    private static float _cpuUtilSumDenominator;

    private static long _cpuUtilStartTicks = -1;

    private static readonly long _cpuUtilPeriod = (long)(1f * Stopwatch.Frequency);

    private static readonly StateMachine _stateMachine;

    private static float _cpuUtilization;

    private static float? _longTermAverageFrameTime;

    private static double _totalGameTime;

    private static double _totalFrameTime;

    private static double _totalCpuFrameTime;

    private static int _frameCount;

    private static string _statsString;

    private static FrameData[] _frameData = [];

    private static int _frameDataIndex;

    static PerformanceManager()
    {
        _primitivesRenderer = new PrimitivesRenderer2D();
        _averageFrameTime = new RunningAverage(1f);
        _statsString = string.Empty;
        _stateMachine = new StateMachine();
        _stateMachine.AddState(
            "PreMeasure",
            delegate { _totalGameTime = 0.0; },
            delegate
            {
                _totalGameTime += Time.FrameDuration;
                if (_totalGameTime > 60.0)
                {
                    _stateMachine.TransitionTo("Measuring");
                }
            },
            Actions.Empty
        );
        _stateMachine.AddState(
            "Measuring",
            delegate
            {
                _totalFrameTime = 0.0;
                _totalCpuFrameTime = 0.0;
                _frameCount = 0;
            },
            delegate
            {
                if (ScreensManager.CurrentScreen == null ||
                    ScreensManager.CurrentScreen.GetType() != typeof(GameScreen))
                {
                    return;
                }

                var lastFrameTime = GameEntry.LastFrameTime;
                var lastCpuFrameTime = GameEntry.LastCpuFrameTime;
                if (lastFrameTime is > 0f and < 1f && lastCpuFrameTime is > 0f and < 1f)
                {
                    _totalFrameTime += lastFrameTime;
                    _totalCpuFrameTime += lastCpuFrameTime;
                    _frameCount++;
                }

                if (_totalFrameTime > 180.0)
                {
                    _stateMachine.TransitionTo("PostMeasure");
                }
            },
            Actions.Empty
        );
        _stateMachine.AddState(
            "PostMeasure",
            delegate
            {
                if (_frameCount <= 0)
                {
                    return;
                }

                _longTermAverageFrameTime = (float)(_totalFrameTime / _frameCount);
                float num = (int)MathUtils.Round(
                    MathUtils.Round(_totalFrameTime / _frameCount / 0.004999999888241291) * 0.004999999888241291 *
                    1000.0);
                float num2 =
                    (int)MathUtils.Round(MathUtils.Round(_totalCpuFrameTime / _frameCount / 0.004999999888241291) *
                                         0.004999999888241291 * 1000.0);
                Log.Information(
                    $"PerformanceManager Measurement: frames={_frameCount}, avgFrameTime={num}ms, avgFrameCpuTime={num2}ms");
            },
            Actions.Empty,
            Actions.Empty
        );
        _stateMachine.TransitionTo("PreMeasure");
    }

    public static float? LongTermAverageFrameTime => _longTermAverageFrameTime;

    private static float AverageFrameTime => _averageFrameTime.Value;

    private static long TotalMemoryUsed { get; set; }

    private static long TotalGpuMemoryUsed { get; set; }

    public static void Update()
    {
        _averageFrameTime.AddSample(GameEntry.LastFrameTime);
        _cpuUtilSumNumerator += GameEntry.LastCpuFrameTime;
        _cpuUtilSumDenominator += GameEntry.LastFrameTime;
        var timestamp = Stopwatch.GetTimestamp();
        if (_cpuUtilStartTicks < 0)
        {
            _cpuUtilStartTicks = timestamp;
        }

        if (timestamp >= _cpuUtilStartTicks + _cpuUtilPeriod)
        {
            if (_cpuUtilSumDenominator > 0f)
            {
                _cpuUtilization = _cpuUtilSumNumerator / _cpuUtilSumDenominator;
            }

            _cpuUtilSumNumerator = 0f;
            _cpuUtilSumDenominator = 0f;
            _cpuUtilStartTicks = timestamp;
        }

        if (Time.PeriodicEvent(1.0, 0.0))
        {
            TotalMemoryUsed = GC.GetTotalMemory(false);
            TotalGpuMemoryUsed = Display.GetGpuMemoryUsage();
        }

        _stateMachine.Update();
    }

    public static void Draw()
    {
        var scale = new Vector2(MathUtils.Round(MathUtils.Clamp(ScreensManager.RootWidget.GlobalScale, 1f, 4f)));
        var viewport = Display.Viewport;
        if (SettingsManager.Current.DisplayDebugInfo)
        {
            if (Time.PeriodicEvent(1.0, 0.0))
            {
                _statsString =
                    $"SCREEN {ScreensManager.GetCurrentScreenName()}, " +
                    $"CPUMEM {TotalMemoryUsed / 1024f / 1024f:0}MB, " +
                    $"GPUMEM {TotalGpuMemoryUsed / 1024f / 1024f:0}MB, " +
                    $"CPU {_cpuUtilization * 100f:0}%, {1f / AverageFrameTime:0.0} FPS";
                if (CommonLib.WorkType == WorkType.Client)
                {
                    if (CommonLib.Net.Server != null)
                    {
                        var p = CommonLib.Net.Server.Peer;
                        if (p != null)
                        {
                            _statsString += $", PL: {p.Statistics.PacketLossPercent}% Ping:{p.Ping}";
                        }
                    }
                }
            }

            _primitivesRenderer.FontBatch(BitmapFont.DebugFont, 0, null, null, null, SamplerState.PointClamp)
                .QueueText(_statsString, new Vector2(0f, 0f), 0f, Color.White, TextAnchor.Left, scale, Vector2.Zero);
        }

        if (SettingsManager.Current.DisplayFpsRibbon)
        {
            var num = viewport.Width / scale.X > 480f ? scale.X * 2f : scale.X;
            var num2 = viewport.Height / -0.1f;
            float num3 = viewport.Height - 1;
            var s = 0.5f;
            var num4 = MathUtils.Max((int)(viewport.Width / num), 1);
            if (_frameData.Length == 0 || _frameData.Length != num4)
            {
                _frameData = new FrameData[num4];
                _frameDataIndex = 0;
            }

            _frameData[_frameDataIndex] = new FrameData
            {
                CpuTime = GameEntry.LastCpuFrameTime,
                TotalTime = GameEntry.LastFrameTime
            };
            _frameDataIndex = (_frameDataIndex + 1) % _frameData.Length;
            var flatBatch2D = _primitivesRenderer.FlatBatch();
            var color = Color.Orange * s;
            var color2 = Color.Red * s;
            for (var num5 = _frameData.Length - 1; num5 >= 0; num5--)
            {
                var num6 = (num5 - _frameData.Length + 1 + _frameDataIndex + _frameData.Length) % _frameData.Length;
                var frameData = _frameData[num6];
                var x = num5 * num;
                var x2 = (num5 + 1) * num;
                flatBatch2D.QueueQuad(new Vector2(x, num3), new Vector2(x2, num3 + frameData.CpuTime * num2), 0f,
                    color);
                flatBatch2D.QueueQuad(new Vector2(x, num3 + frameData.CpuTime * num2),
                    new Vector2(x2, num3 + frameData.TotalTime * num2), 0f, color2);
            }

            flatBatch2D.QueueLine(new Vector2(0f, num3 + 0.0166666675f * num2),
                new Vector2(viewport.Width, num3 + 0.0166666675f * num2), 0f, Color.Green);
        }
        else
        {
            _frameData = [];
        }

        _primitivesRenderer.Flush();
    }

    private struct FrameData
    {
        public float CpuTime;

        public float TotalTime;
    }
}
