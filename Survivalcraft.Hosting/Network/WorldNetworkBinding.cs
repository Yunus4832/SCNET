using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using EntitySystem.Core;

using Game.Network.Handling;

namespace Game.Network;

/// <summary>Actual per-world routing state owned by networking, not the Core world lifetime.</summary>
public sealed class WorldNetworkBinding : IDisposable
{
    private static readonly ConditionalWeakTable<WorldRuntimeContext, WorldNetworkBinding> _bindings = new();

    private readonly Dictionary<PackageHandlerRegistration, IPackageHandler> _handlers = [];

    private readonly CancellationTokenRegistration _closed;

    private bool _disposed;

    public WorldRuntimeContext World { get; }

    public NetNode? Node { get; }

    public PackageHandlerCatalog Handlers { get; private set; }

    private bool _worldHandlersInstalled;

    public RuntimeMessageInbox Inbox { get; }

    public WorldNetworkBinding(WorldRuntimeContext world, NetNode? node, PackageHandlerCatalog handlers)
    {
        ObjectDisposedException.ThrowIf(world.IsClosed, world);
        World = world;
        Node = node;
        Handlers = handlers;
        Inbox = new RuntimeMessageInbox(world);
        _bindings.Add(world, this);
        try
        {
            world.OwnWorldResource(this);
            _ = new WorldMessageEndpoint(world, new WorldMessageSink(this));
            _closed = world.Lifetime.Register(ClearRouting);
        }
        catch
        {
            _bindings.Remove(world);
            throw;
        }
    }

    public static WorldNetworkBinding For(WorldRuntimeContext world)
    {
        return _bindings.TryGetValue(world, out var binding)
            ? binding
            : throw new InvalidOperationException("World has no networking binding.");
    }

    public static WorldNetworkBinding For(Project project) => For(WorldRuntimeContext.For(project));

    public static bool TryGet(WorldRuntimeContext world, [NotNullWhen(true)] out WorldNetworkBinding? binding)
    {
        return _bindings.TryGetValue(world, out binding);
    }

    public static NetNode? FindNode(WorldRuntimeContext? world)
    {
        return world != null && _bindings.TryGetValue(world, out var binding) ? binding.Node : null;
    }

    public static NetNode? FindNode(Project project) => FindNode(WorldRuntimeContext.For(project));

    public NetNode RequireNode()
    {
        return Node ?? throw new InvalidOperationException("Routing scope has no physical message node.");
    }

    public void InstallHandlers(PackageHandlerCatalog handlers)
    {
        ObjectDisposedException.ThrowIf(World.IsClosed || _disposed, this);
        if (_worldHandlersInstalled || World.Project is not null)
        {
            throw new InvalidOperationException("World handlers must be installed once before project construction.");
        }

        ArgumentNullException.ThrowIfNull(handlers);
        _handlers.Clear();
        Handlers = handlers;
        _worldHandlersInstalled = true;
    }

    public bool AllowsMessage(IProtocolMessage message) =>
        (_worldHandlersInstalled || ProtocolReceivePolicy.IsPreWorldMessage(message)) &&
        Handlers.Contains(message.GetType());

    public IPackageHandler<TPackage> Resolve<TPackage>() where TPackage : IProtocolMessage
    {
        ObjectDisposedException.ThrowIf(World.IsClosed || _disposed, this);
        return (IPackageHandler<TPackage>)ResolveHandler(Handlers.ResolveFamily(typeof(TPackage)));
    }

    public bool Dispatch(IProtocolMessage message, PackageReceiveContext context)
    {
        if (World.IsClosed || !ReferenceEquals(context.Runtime, World)) { return false; }

        if (!AllowsMessage(message) || !Handlers.TryResolve(message, out var registration)) { return false; }

        ResolveHandler(registration!).Handle(message, context);
        return true;
    }

    public bool Dispatch(ReceivedPackage received) => Dispatch(received.Package, received.Context);

    internal IPackageHandler ResolveHandler(PackageHandlerRegistration registration)
    {
        ObjectDisposedException.ThrowIf(World.IsClosed, World);
        if (!_handlers.TryGetValue(registration, out var handler))
        {
            handler = registration.CreateHandler();
            _handlers.Add(registration, handler);
        }

        return handler;
    }

    private void ClearRouting()
    {
        _handlers.Clear();
        Handlers = PackageHandlerCatalog.Empty;
        Inbox.Clear();
    }

    public void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        try
        {
            _closed.Dispose();
            ClearRouting();
        }
        finally { _bindings.Remove(World); }
    }
}
