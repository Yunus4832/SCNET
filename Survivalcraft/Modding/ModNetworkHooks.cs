using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;
using Game.Network.Serialization;

namespace Game.Modding;

public interface IModNetwork
{
    IDisposable OnMessage(string messageType, Action<ModNetworkMessageContext> handler, int priority = 0);

    void Send(
        string messageType,
        Action<PackageStreamWriter> writePayload,
        PackageAudience audience,
        ClientState minNeedState = ClientState.Connected);

    void Send(
        string messageType,
        byte[] payload,
        PackageAudience audience,
        ClientState minNeedState = ClientState.Connected);
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
            try
            {
                using var reader = new PackageStreamReader(package.Payload);
                var context = new ModNetworkMessageContext(
                    this,
                    handler.Owner,
                    package.MessageType,
                    reader,
                    receiveContext.Sender,
                    receiveContext.Node,
                    receiveContext.IsServer);
                handler.Handler(context);
            }
            catch (Exception exception)
            {
                Log.Error($"Mod {handler.Owner} network handler failed: {exception}");
            }
        }
    }

    internal IModNetwork ForOwner(ModId owner) => new OwnedModNetwork(owner, this);

    internal void Freeze()
    {
        _isFrozen = true;
        RebuildRoutes();
    }

    internal void RemoveOwner(ModId owner)
    {
        _registrations.RemoveAll(registration => registration.Owner == owner);
        RebuildRoutes();
    }

    internal void Send(
        ModId owner,
        string messageType,
        Action<PackageStreamWriter> writePayload,
        PackageAudience audience,
        ClientState minNeedState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentNullException.ThrowIfNull(writePayload);
        ArgumentNullException.ThrowIfNull(audience);
        using var writer = new PackageStreamWriter();
        writePayload(writer);
        Send(owner, messageType, writer.Data(), audience, minNeedState);
    }

    internal void Send(
        ModId owner,
        string messageType,
        byte[] payload,
        PackageAudience audience,
        ClientState minNeedState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(audience);
        if (CommonLib.Net == null)
        {
            throw new InvalidOperationException("Network is not initialized.");
        }

        var package = new ModEnvelopePackage(owner.ToString(), messageType, payload)
        {
            RequiredState = minNeedState
        };
        CommonLib.Net.QueuePackage(package, audience);
    }

    private IDisposable Register(
        ModId owner,
        string messageType,
        Action<ModNetworkMessageContext> handler,
        int priority)
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
            foreach (var messageGroup in group.GroupBy(registration => registration.MessageType,
                         StringComparer.Ordinal))
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

        public void Send(
            string messageType,
            Action<PackageStreamWriter> writePayload,
            PackageAudience audience,
            ClientState minNeedState = ClientState.Connected) =>
            hooks.Send(owner, messageType, writePayload, audience, minNeedState);

        public void Send(
            string messageType,
            byte[] payload,
            PackageAudience audience,
            ClientState minNeedState = ClientState.Connected) =>
            hooks.Send(owner, messageType, payload, audience, minNeedState);
    }

    private sealed class Registration(
        ModNetworkHooks hooks,
        ModId owner,
        string messageType,
        Action<ModNetworkMessageContext> handler,
        int priority,
        long sequence) : IDisposable
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
    PackageStreamReader reader,
    Client? from,
    NetNode? netNode,
    bool isServer)
{
    public ModId Owner { get; } = owner;

    public string MessageType { get; } = messageType;

    public PackageStreamReader Reader { get; } = reader;

    public Client? From { get; } = from;

    public NetNode? NetNode { get; } = netNode;

    public bool IsServer { get; } = isServer;

    public void Reply(
        Action<PackageStreamWriter> writePayload,
        ClientState minNeedState = ClientState.Connected)
    {
        if (From == null)
        {
            return;
        }

        hooks.Send(Owner, MessageType, writePayload, PackageAudience.To(From), minNeedState);
    }

    public void Send(
        string targetMessageType,
        Action<PackageStreamWriter> writePayload,
        PackageAudience audience,
        ClientState minNeedState = ClientState.Connected)
    {
        hooks.Send(Owner, targetMessageType, writePayload, audience, minNeedState);
    }
}
