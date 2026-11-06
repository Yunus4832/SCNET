using EntitySystem.Core;

using Game.World.Timing;

namespace Game.Subsystems;

public class SubsystemTime : Subsystem
{
    public const float MaxGameTimeDelta = 0.1f;

    private readonly List<DelayedExecutionRequest> _delayedExecutionsRequests = [];

    private float _gameTimeFactor = 1f;

    public double GameTime { get; private set; }

    public float GameTimeDelta { get; private set; }

    public float PreviousGameTimeDelta { get; private set; }

    public float GameTimeFactor
    {
        get => _gameTimeFactor;
        private set => _gameTimeFactor = MathUtils.Clamp(value, 0f, 256f);
    }

    public float? FixedTimeStep { get; set; }

    public int NextFrame(float frameDuration, Func<CharacterSleepPopulation> readPopulation)
    {
        if (Project.IsDisposed)
        {
            return 1;
        }

        var runtime = WorldRuntimeContext.For(Project);
        if (runtime.IsClosed) { return 1; }

        PreviousGameTimeDelta = GameTimeDelta;
        GameTimeDelta = !FixedTimeStep.HasValue
            ? MathUtils.Min(frameDuration * _gameTimeFactor, 0.1f)
            : MathUtils.Min(FixedTimeStep.Value * _gameTimeFactor, 0.1f);

        GameTime += GameTimeDelta;
        var num = 0;
        while (num < _delayedExecutionsRequests.Count)
        {
            var delayedExecutionRequest = _delayedExecutionsRequests[num];
            if (delayedExecutionRequest.GameTime >= 0.0 && GameTime >= delayedExecutionRequest.GameTime)
            {
                _delayedExecutionsRequests.RemoveAt(num);
                delayedExecutionRequest.Action();
                if (Project.IsDisposed || runtime.IsClosed)
                {
                    return 1;
                }
            }
            else
            {
                num++;
            }
        }

        var population = runtime.IsAuthority ? readPopulation() : default;
        if (runtime.IsAuthority &&
            population.Sleeping + population.Dead == population.Total &&
            population.Sleeping >= 1)
        {
            FixedTimeStep = 0.05f;
            return 20;
        }

        FixedTimeStep = null;
        return 1;
    }

    public void SetPaused(bool paused) => GameTimeFactor = paused ? 0f : 1f;

    public void QueueGameTimeDelayedExecution(double gameTime, Action action)
    {
        if (Project.IsDisposed || WorldRuntimeContext.For(Project).IsClosed) { return; }

        _delayedExecutionsRequests.Add(
            new DelayedExecutionRequest
            {
                GameTime = gameTime,
                Action = action
            }
        );
    }

    public bool PeriodicGameTimeEvent(double period, double offset)
    {
        var num = GameTime - offset;
        var num2 = MathUtils.Floor(num / period) * period;
        if (num >= num2)
        {
            return num - GameTimeDelta < num2;
        }

        return false;
    }

    private struct DelayedExecutionRequest
    {
        public double GameTime;

        public Action Action;
    }
}
