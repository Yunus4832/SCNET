using Game.Network.Transport;

namespace Game.Network.Handling;

public static class PackageDispatcher
{
    private static readonly Dictionary<Type, IPackageHandler> _handlers = new();

    public static void Register(IPackageHandler handler)
    {
        _handlers[handler.PackageType] = handler;
    }

    internal static void Register(Type messageType, IPackageHandler handler)
    {
        if (!handler.PackageType.IsAssignableFrom(messageType)) { throw new ArgumentException("Handler cannot accept this message type."); }
        _handlers[messageType] = handler;
    }

    public static void Register<TPackage>(IPackageHandler<TPackage> handler) where TPackage : IProtocolMessage
    {
        Register((IPackageHandler)handler);
    }

    public static void Unregister(Type packageType)
    {
        var owner = _handlers.TryGetValue(packageType, out var handler) ? handler.PackageType : packageType;
        foreach (var key in _handlers.Where(item => item.Value.PackageType == owner).Select(item => item.Key).ToArray())
        {
            _handlers.Remove(key);
        }
    }

    public static bool TryHandle(IProtocolMessage package, PackageReceiveContext context)
    {
        var packageType = package.GetType();
        if (_handlers.TryGetValue(packageType, out var handler))
        {
            handler.Handle(package, context);
            return true;
        }

        return false;
    }

    public static void Handle(ReceivedPackage received)
    {
        Handle(received.Package, received.Context);
    }

    public static void Handle(IProtocolMessage package, PackageReceiveContext context)
    {
        if (!TryHandle(package, context))
        {
            Log.Information($"未注册Package处理器:{package.GetType().Name}");
        }
    }
}
