using EntitySystem.Core;

namespace Game.Modding;

public interface IModNetwork
{
    IDisposable OnMessage(string messageType, Action<ModNetworkMessageContext> handler, int priority = 0);

    void Send<T>(
        WorldRuntimeContext runtime,
        string messageType,
        T payload,
        PackageAudience audience,
        ClientState minNeedState = ClientState.Connected
    );

    void Send(
        WorldRuntimeContext runtime,
        string messageType,
        byte[] payload,
        PackageAudience audience,
        ClientState minNeedState = ClientState.Connected
    );
}

public sealed class ModNetworkHooks
{
    private readonly List<Registration> _registrations = [];

    private readonly Dictionary<ModId, Dictionary<string, Registration[]>> _routes = [];

    private bool _isFrozen;

    private long _sequence;

    public IReadOnlyList<ModNetworkRegistrationInfo> Registrations => _registrations
        .Select(registration => new ModNetworkRegistrationInfo(registration.Owner, registration.MessageType))
        .ToArray();

    public void Dispatch(ModEnvelopePackage package, PackageReceiveContext receiveContext)
    {
        if (receiveContext.Runtime.IsClosed)
        {
            return;
        }

        ModId owner;
        try
        {
            owner = new ModId(package.ModId);
        }
        catch (Exception exception)
        {
            Log.Error($"Ignoring mod network packet with invalid mod id \"{package.ModId}\": {exception.Message}");
            return;
        }

        if (!_routes.TryGetValue(owner, out var ownerRoutes) ||
            !ownerRoutes.TryGetValue(package.MessageType, out var handlers))
        {
            return;
        }

        foreach (var handler in handlers)
        {
            if (receiveContext.Runtime.IsClosed) { break; }

            try
            {
                var context = new ModNetworkMessageContext(
                    this,
                    handler.Owner,
                    package.MessageType,
                    package.Payload,
                    receiveContext
                );
                handler.Handler(context);
            }
            catch (Exception exception)
            {
                Log.Error($"Mod {handler.Owner} network handler failed: {exception}");
            }
        }
    }

    public IModNetwork ForOwner(ModId owner) => new OwnedModNetwork(owner, this);

    public void Freeze()
    {
        _isFrozen = true;
        RebuildRoutes();
    }

    public void RemoveOwner(ModId owner)
    {
        _registrations.RemoveAll(registration => registration.Owner == owner);
        RebuildRoutes();
    }

    internal void Send<T>(
        ModId owner,
        WorldRuntimeContext runtime,
        string messageType,
        T payload,
        PackageAudience audience,
        ClientState minNeedState
    )
    {
        EnqueueOwned(owner, runtime, messageType, ProtocolPayloadCodec.Encode(payload), audience, minNeedState);
    }

    internal void Send(
        ModId owner,
        WorldRuntimeContext runtime,
        string messageType,
        byte[] payload,
        PackageAudience audience,
        ClientState minNeedState
    )
    {
        ArgumentNullException.ThrowIfNull(payload);
        EnqueueOwned(owner, runtime, messageType, payload.ToArray(), audience, minNeedState);
    }

    private static void EnqueueOwned(
        ModId owner,
        WorldRuntimeContext runtime,
        string messageType,
        byte[] payload,
        PackageAudience audience,
        ClientState minNeedState
    )
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(audience);
        if (runtime.IsClosed)
        {
            return;
        }

        var node = WorldNetworkBinding.For(runtime).Node ??
                   throw new InvalidOperationException("The mod message has no runtime endpoint.");

        var package = new ModEnvelopePackage(owner.ToString(), messageType, payload)
        {
            RequiredState = minNeedState
        };
        ProtocolMessageValidation.Validate(package);
        node.QueuePackage(runtime, package, audience);
    }

    private IDisposable Register(
        ModId owner,
        string messageType,
        Action<ModNetworkMessageContext> handler,
        int priority
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentNullException.ThrowIfNull(handler);
        if (_isFrozen)
        {
            throw new InvalidOperationException("Mod network hooks are frozen.");
        }

        var registration = new Registration(this, owner, messageType, handler, priority, _sequence++);
        _registrations.Add(registration);
        return registration;
    }

    private void Remove(Registration registration)
    {
        _registrations.Remove(registration);
        RebuildRoutes();
    }

    private void RebuildRoutes()
    {
        _routes.Clear();
        foreach (var group in _registrations.GroupBy(registration => registration.Owner))
        {
            var messageRoutes = new Dictionary<string, Registration[]>(StringComparer.Ordinal);
            foreach (var messageGroup in group.GroupBy(
                         registration => registration.MessageType,
                         StringComparer.Ordinal
                     ))
            {
                messageRoutes[messageGroup.Key] = messageGroup
                    .OrderByDescending(registration => registration.Priority)
                    .ThenBy(registration => registration.Sequence)
                    .ToArray();
            }

            _routes[group.Key] = messageRoutes;
        }
    }

    private sealed class OwnedModNetwork(ModId owner, ModNetworkHooks hooks) : IModNetwork
    {
        public IDisposable OnMessage(string messageType, Action<ModNetworkMessageContext> handler, int priority = 0) =>
            hooks.Register(owner, messageType, handler, priority);

        public void Send<T>(
            WorldRuntimeContext runtime,
            string messageType,
            T payload,
            PackageAudience audience,
            ClientState minNeedState = ClientState.Connected
        ) =>
            hooks.Send(owner, runtime, messageType, payload, audience, minNeedState);

        public void Send(
            WorldRuntimeContext runtime,
            string messageType,
            byte[] payload,
            PackageAudience audience,
            ClientState minNeedState = ClientState.Connected
        ) =>
            hooks.Send(owner, runtime, messageType, payload, audience, minNeedState);
    }

    private sealed class Registration(
        ModNetworkHooks hooks,
        ModId owner,
        string messageType,
        Action<ModNetworkMessageContext> handler,
        int priority,
        long sequence
    ) : IDisposable
    {
        private ModNetworkHooks? _hooks = hooks;

        public ModId Owner { get; } = owner;

        public string MessageType { get; } = messageType;

        public Action<ModNetworkMessageContext> Handler { get; } = handler;

        public int Priority { get; } = priority;

        public long Sequence { get; } = sequence;

        public void Dispose() => Interlocked.Exchange(ref _hooks, null)?.Remove(this);
    }
}

public sealed record ModNetworkRegistrationInfo(ModId Owner, string MessageType);

public sealed class ModNetworkMessageContext(
    ModNetworkHooks hooks,
    ModId owner,
    string messageType,
    ReadOnlyMemory<byte> payload,
    PackageReceiveContext receiveContext
)
{
    public ModId Owner { get; } = owner;

    public string MessageType { get; } = messageType;

    public ReadOnlyMemory<byte> Payload { get; } = payload;

    public T Read<T>() => ProtocolPayloadCodec.Decode<T>(Payload);

    public WorldConnection? From => receiveContext.Sender;

    public WorldRuntimeContext Runtime => receiveContext.Runtime;

    public Project? Project => Runtime.Project;

    public NetNode? NetNode => receiveContext.Node;

    public bool IsServer => Runtime.IsAuthority;

    public void Reply<T>(
        T value,
        ClientState minNeedState = ClientState.Connected
    )
    {
        if (From == null)
        {
            return;
        }

        hooks.Send(Owner, Runtime, MessageType, value, PackageAudience.To(From), minNeedState);
    }

    public void Send<T>(
        string targetMessageType,
        T value,
        PackageAudience audience,
        ClientState minNeedState = ClientState.Connected
    )
    {
        hooks.Send(Owner, Runtime, targetMessageType, value, audience, minNeedState);
    }
}
