using Engine.Core.Diagnostics;
using Engine.Graphics;

using Game.Application;

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

    private static FrameData[] _frameData = [];

    private static int _frameDataIndex;

    static PerformanceManager()
    {
        _primitivesRenderer = new PrimitivesRenderer2D();
        _averageFrameTime = new RunningAverage(1f);
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
                    ScreensManager.GetCurrentScreenName() != "Game")
                {
                    return;
                }

                var lastFrameTime = ClientApplication.LastFrameTime;
                var lastCpuFrameTime = ClientApplication.LastCpuFrameTime;
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
                float averageFrameMs = (int)MathUtils.Round(
                    MathUtils.Round(_totalFrameTime / _frameCount / 0.004999999888241291) *
                    0.004999999888241291 *
                    1000.0
                );
                float averageCpuMs =
                    (int)MathUtils.Round(
                        MathUtils.Round(_totalCpuFrameTime / _frameCount / 0.004999999888241291) *
                        0.004999999888241291 *
                        1000.0
                    );
                Log.Information(
                    $"PerformanceManager Measurement: frames={_frameCount}, avgFrameTime={averageFrameMs}ms, avgFrameCpuTime={averageCpuMs}ms"
                );
            },
            Actions.Empty,
            Actions.Empty
        );
        _stateMachine.TransitionTo("PreMeasure");
    }

    public static float? LongTermAverageFrameTime => _longTermAverageFrameTime;

    public static float AverageFrameTime => _averageFrameTime.Value;

    public static float CpuUtilization => _cpuUtilization;

    public static long TotalMemoryUsed { get; private set; }

    public static long TotalGpuMemoryUsed { get; private set; }

    public static void Update()
    {
        _averageFrameTime.AddSample(ClientApplication.LastFrameTime);
        _cpuUtilSumNumerator += ClientApplication.LastCpuFrameTime;
        _cpuUtilSumDenominator += ClientApplication.LastFrameTime;
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
        if (Configuration.ClientConfiguration.Preferences.DisplayFpsRibbon)
        {
            var barWidth = viewport.Width / scale.X > 480f ? scale.X * 2f : scale.X;
            var secondsToPixels = viewport.Height / -0.1f;
            float baselineY = viewport.Height - 1;
            var s = 0.5f;
            var barCount = MathUtils.Max((int)(viewport.Width / barWidth), 1);
            if (_frameData.Length == 0 || _frameData.Length != barCount)
            {
                _frameData = new FrameData[barCount];
                _frameDataIndex = 0;
            }

            _frameData[_frameDataIndex] = new FrameData
            {
                CpuTime = ClientApplication.LastCpuFrameTime,
                TotalTime = ClientApplication.LastFrameTime
            };
            _frameDataIndex = (_frameDataIndex + 1) % _frameData.Length;
            var flatBatch2D = _primitivesRenderer.FlatBatch();
            var color = Color.Orange * s;
            var color2 = Color.Red * s;
            for (var barIndex = _frameData.Length - 1; barIndex >= 0; barIndex--)
            {
                var frameIndex = (barIndex - _frameData.Length + 1 + _frameDataIndex + _frameData.Length) %
                                 _frameData.Length;
                var frameData = _frameData[frameIndex];
                var x = barIndex * barWidth;
                var x2 = (barIndex + 1) * barWidth;
                flatBatch2D.QueueQuad(
                    new Vector2(x, baselineY),
                    new Vector2(x2, baselineY + frameData.CpuTime * secondsToPixels),
                    0f,
                    color
                );
                flatBatch2D.QueueQuad(
                    new Vector2(x, baselineY + frameData.CpuTime * secondsToPixels),
                    new Vector2(x2, baselineY + frameData.TotalTime * secondsToPixels),
                    0f,
                    color2
                );
            }

            flatBatch2D.QueueLine(
                new Vector2(0f, baselineY + 0.0166666675f * secondsToPixels),
                new Vector2(viewport.Width, baselineY + 0.0166666675f * secondsToPixels),
                0f,
                Color.Green
            );
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
