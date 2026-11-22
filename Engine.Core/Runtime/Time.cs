using System.Diagnostics;

using Engine.Core.Mathematics;

namespace Engine.Core.Runtime;

public static class Time
{
    private static readonly long _applicationStartTicks = Stopwatch.GetTimestamp();

    private static readonly List<DelayedExecutionRequest> _delayedExecutionsRequests = [];

    public static int FrameIndex { get; private set; }

    public static double RealTime
    {
        get
        {
            var origin = _applicationStartTicks;
            return (Stopwatch.GetTimestamp() - origin) / (double)Stopwatch.Frequency;
        }
    }

    public static double PreviousFrameStartTime { get; private set; }

    public static double FrameStartTime { get; private set; }

    public static float PreviousFrameDuration { get; private set; }

    public static float FrameDuration { get; private set; }

    public static bool PeriodicEvent(double period, double offset)
    {
        var offsetTime = FrameStartTime - offset;
        var periodStart = MathUtils.Floor(offsetTime / period) * period;
        if (offsetTime >= periodStart)
        {
            return offsetTime - FrameDuration < periodStart;
        }

        return false;
    }

    public static void QueueTimeDelayedExecution(double time, Action action)
    {
        _delayedExecutionsRequests.Add(
            new DelayedExecutionRequest
            {
                Time = time,
                FramesCount = -1,
                Action = action
            }
        );
    }

    public static void QueueFrameIndexDelayedExecution(int framesCount, Action action)
    {
        _delayedExecutionsRequests.Add(
            new DelayedExecutionRequest
            {
                Time = -1.0,
                FramesCount = framesCount,
                Action = action
            }
        );
    }

    public static void BeforeFrame()
    {
        var realTime = RealTime;
        PreviousFrameDuration = FrameDuration;
        FrameDuration = (float)(realTime - FrameStartTime);
        PreviousFrameStartTime = FrameStartTime;
        FrameStartTime = realTime;
        var requestIndex = 0;
        while (requestIndex < _delayedExecutionsRequests.Count)
        {
            var delayedExecutionRequest = _delayedExecutionsRequests[requestIndex];
            if ((delayedExecutionRequest.Time >= 0.0 && FrameStartTime >= delayedExecutionRequest.Time) ||
                (delayedExecutionRequest.FramesCount >= 0 && FrameIndex >= delayedExecutionRequest.FramesCount))
            {
                _delayedExecutionsRequests.RemoveAt(requestIndex);
                delayedExecutionRequest.Action();
            }
            else
            {
                requestIndex++;
            }
        }
    }

    public static void AfterFrame()
    {
        FrameIndex++;
    }

    private struct DelayedExecutionRequest
    {
        public double Time;

        public int FramesCount;

        public Action Action;
    }
}
