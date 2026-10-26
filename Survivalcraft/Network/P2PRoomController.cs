using Engine.Network;

using Game.Servers;

using Rendezvous.Protocol;

namespace Game.Network;

/// <summary>Owns a room registration; the listener and established connections belong to NetNode.</summary>
internal sealed class P2PRoomController(
    Func<NetworkHost, P2PRoomSettings, int, HttpClient, CancellationToken, Task<NetworkRoomRegistration>>? register =
        null,
    Func<DateTimeOffset>? now = null
)
{
    private sealed class Attempt(NetworkHost host, P2PRoomSettings configuration, int capacity, long generation)
    {
        public NetworkHost Host { get; } = host;

        public P2PRoomSettings Configuration { get; } = configuration;

        public int Capacity { get; } = capacity;

        public long Generation { get; } = generation;

        public HttpClient Http { get; } = new();

        public CancellationTokenSource Cancellation { get; } = new();

        public Task<NetworkRoomRegistration> Opening { get; set; } = null!;

        public NetworkRoomRegistration? Registration { get; set; }
    }

    private readonly
        Func<NetworkHost, P2PRoomSettings, int, HttpClient, CancellationToken, Task<NetworkRoomRegistration>>
        _register =
            register ?? RegisterAsync;

    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.UtcNow);

    private NetworkHost? _host;

    private Attempt? _attempt;

    private Task<Exception?>? _closing;

    private long _generation;

    private int _retryCount;

    public P2PRoomState State { get; private set; }

    public bool DesiredEnabled { get; private set; }

    public bool AutomaticPending { get; private set; }

    public P2PRoomSettings Configuration { get; private set; } = new();

    public int Capacity { get; private set; }

    public string LastError { get; private set; } = string.Empty;

    public DateTimeOffset? NextRetryAt { get; private set; }

    public NetworkRoomRegistration? Registration => _attempt?.Registration;

    public void Bind(NetworkHost host, P2PRoomSettings configuration, int capacity)
    {
        if (_host is not null)
        {
            throw new InvalidOperationException("A room controller is already bound to a listener.");
        }

        _host = host;
        Configuration = configuration;
        Capacity = Math.Clamp(capacity, 1, 32);
        AutomaticPending = configuration.Enabled;
    }

    public void ConsumeAutomaticRequest() => AutomaticPending = false;

    public void Start(P2PRoomSettings configuration)
    {
        if (_host is null || State is not P2PRoomState.Disabled)
        {
            throw new InvalidOperationException("The room must be disabled and have an active listener.");
        }

        Configuration = configuration;
        DesiredEnabled = true;
        ConsumeAutomaticRequest();
        LastError = string.Empty;
        _retryCount = 0;
        ++_generation;
        BeginRegistration();
    }

    public void Stop()
    {
        ConsumeAutomaticRequest();
        DesiredEnabled = false;
        ++_generation;
        NextRetryAt = null;
        if (State is P2PRoomState.Stopping or P2PRoomState.Disabled)
        {
            return;
        }

        LastError = string.Empty;
        RetireAttempt();
        State = _closing is null ? P2PRoomState.Disabled : P2PRoomState.Stopping;
    }

    public async Task ShutdownAsync()
    {
        Stop();
        var closing = _closing;
        _host = null;
        if (closing is not null && await closing.ConfigureAwait(false) is { } error)
        {
            Log.Warning($"Could not close P2P room: {error.Message}");
        }
    }

    public void Update()
    {
        if (_closing is { IsCompleted: true } closing)
        {
            _closing = null;
            if (closing.GetAwaiter().GetResult() is { } error)
            {
                LastError = error.Message;
                Log.Warning($"Could not close P2P room: {error.Message}");
            }

            if (!DesiredEnabled)
            {
                State = P2PRoomState.Disabled;
            }
        }

        if (_attempt is { Registration: null, Opening.IsCompleted: true } attempt)
        {
            try
            {
                var registration = attempt.Opening.GetAwaiter().GetResult();
                attempt.Registration = registration;
                if (!DesiredEnabled || attempt.Generation != _generation || !ReferenceEquals(attempt.Host, _host))
                {
                    RetireAttempt();
                }
                else
                {
                    State = P2PRoomState.Active;
                    LastError = string.Empty;
                    NextRetryAt = null;
                    _retryCount = 0;
                    Log.Information(
                        RunMode.Value == RunModeType.HeadlessServer
                            ? $"P2P room code: {registration.JoinCode}"
                            : "P2P room registered"
                    );
                }
            }
            catch (Exception exception)
            {
                Retry(exception);
            }
        }

        if (_attempt?.Registration is { IsActive: false } retired)
        {
            Retry(retired.Failure ?? new TimeoutException("Room registration expired."));
        }

        if (DesiredEnabled &&
            State is P2PRoomState.RetryWaiting &&
            _closing is null &&
            NextRetryAt <= _now())
        {
            BeginRegistration();
        }
    }

    private void BeginRegistration()
    {
        var attempt = new Attempt(_host!, Configuration, Capacity, _generation);
        _attempt = attempt;
        State = P2PRoomState.Registering;
        NextRetryAt = null;
        try
        {
            attempt.Opening = _register(
                attempt.Host,
                attempt.Configuration,
                attempt.Capacity,
                attempt.Http,
                attempt.Cancellation.Token
            );
        }
        catch (Exception exception)
        {
            attempt.Opening = Task.FromException<NetworkRoomRegistration>(exception);
        }
    }

    private void Retry(Exception exception)
    {
        LastError = exception.Message;
        _retryCount = Math.Min(_retryCount + 1, 4);
        var delay = Math.Min(30, 2 << _retryCount);
        NextRetryAt = _now() + TimeSpan.FromSeconds(delay);
        RetireAttempt();
        State = P2PRoomState.RetryWaiting;
        Log.Warning($"P2P room unavailable; retrying in {delay}s: {exception.Message}");
    }

    private void RetireAttempt()
    {
        if (_attempt is not { } attempt)
        {
            return;
        }

        _attempt = null;
        attempt.Cancellation.Cancel();
        _closing = CloseAsync(attempt);
    }

    private static async Task<Exception?> CloseAsync(Attempt attempt)
    {
        try
        {
            NetworkRoomRegistration registration;
            try
            {
                registration = await attempt.Opening.ConfigureAwait(false);
            }
            catch
            {
                // RegisterAsync cleans any partially created room before propagating its failure.
                return null;
            }

            await registration.DisposeAsync().ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
        finally
        {
            attempt.Cancellation.Dispose();
            attempt.Http.Dispose();
        }
    }

    private static async Task<NetworkRoomRegistration> RegisterAsync(
        NetworkHost host,
        P2PRoomSettings configuration,
        int capacity,
        HttpClient http,
        CancellationToken cancellationToken
    )
    {
        var registration = await host
            .RegisterRoomAsync(new Uri(configuration.ControlUrl), http, capacity, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await registration.UpdateRoomAsync(
                new RoomPublication(
                    configuration.Name,
                    configuration.Description,
                    configuration.IsPublic
                ),
                cancellationToken
            ).ConfigureAwait(false);
            return registration;
        }
        catch
        {
            try
            {
                await registration.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                Log.Warning($"Could not close failed P2P room registration: {exception.Message}");
            }

            throw;
        }
    }
}
