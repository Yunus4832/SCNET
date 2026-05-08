using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemTime : Subsystem
{
    public const float MaxGameTimeDelta = 0.1f;

    private readonly List<DelayedExecutionRequest> _delayedExecutionsRequests = [];

    private float _gameTimeFactor = 1f;

    private SubsystemPlayers _subsystemPlayers = null!;

    private SubsystemUpdate _subsystemUpdate = null!;

    public double GameTime { get; private set; }

    public float GameTimeDelta { get; private set; }

    public float PreviousGameTimeDelta { get; private set; }

    public float GameTimeFactor
    {
        get => _gameTimeFactor;
        private set => _gameTimeFactor = MathUtils.Clamp(value, 0f, 256f);
    }

    public float? FixedTimeStep { get; set; }

    public void NextFrame()
    {
        PreviousGameTimeDelta = GameTimeDelta;
        GameTimeDelta = !FixedTimeStep.HasValue
            ? MathUtils.Min(Time.FrameDuration * _gameTimeFactor, 0.1f)
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
            }
            else
            {
                num++;
            }
        }

        var num2 = 0;
        var num3 = 0;
        foreach (var componentPlayer in _subsystemPlayers.ComponentPlayers)
        {
            if (componentPlayer.ComponentHealth.Health == 0f)
            {
                num3++;
            }
            else if (componentPlayer.ComponentSleep.SleepFactor.CloseTo(1f))
            {
                num2++;
            }
        }

        if (num2 + num3 == _subsystemPlayers.ComponentPlayers.Count && num2 >= 1)
        {
            FixedTimeStep = 0.05f;
            _subsystemUpdate.UpdatesPerFrame = 20;
        }
        else
        {
            FixedTimeStep = null;
            _subsystemUpdate.UpdatesPerFrame = 1;
        }

        var flag = true;
        foreach (var componentPlayer2 in _subsystemPlayers.ComponentPlayers)
        {
            if (!componentPlayer2.ComponentGui.IsGameMenuDialogVisible())
            {
                flag = false;
                break;
            }
        }

        if (flag)
        {
            GameTimeFactor = 0f;
        }
        else if (GameTimeFactor == 0f)
        {
            GameTimeFactor = 1f;
        }
    }

    public void QueueGameTimeDelayedExecution(double gameTime, Action action)
    {
        _delayedExecutionsRequests.Add(new DelayedExecutionRequest
        {
            GameTime = gameTime,
            Action = action
        });
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

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true)!;
        _subsystemUpdate = Project.FindSubsystem<SubsystemUpdate>(true)!;
    }

    private struct DelayedExecutionRequest
    {
        public double GameTime;

        public Action Action;
    }
}
