namespace Game;

public class StateMachine
{
    private State? _currentState;

    private State? _previousState;

    private readonly Dictionary<string, State> _states = new();

    public string PreviousState => _previousState?.Name ?? string.Empty;

    public string CurrentState => _currentState?.Name ?? string.Empty;

    public event Action<string>? OnTransitionChange;

    public void AddState(string name, Action enter, Action update, Action leave)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new InvalidOperationException("State name must not be empty or null.");
        }

        _states.Add(name, new State
        {
            Name = name,
            Enter = enter,
            Update = update,
            Leave = leave
        });
    }

    public void TransitionTo(string stateName)
    {
        var state = FindState(stateName);
        if (state == _currentState)
        {
            return;
        }

        _currentState?.Leave();
        _previousState = _currentState;
        _currentState = state;
        _currentState?.Enter();
        OnTransitionChange?.Invoke(stateName);
    }

    public void Update()
    {
        _currentState?.Update();
    }

    public State? FindState(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return !_states.TryGetValue(name, out var value)
            ? throw new InvalidOperationException($"State \"{name}\" not found.")
            : value;
    }

    public class State
    {
        public required Action Enter;

        public required Action Leave;

        public required string Name;

        public required Action Update;
    }
}
