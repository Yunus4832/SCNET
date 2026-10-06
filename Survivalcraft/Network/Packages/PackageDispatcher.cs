namespace Game.Network.Packages;

public static class PackageDispatcher
{
    private static readonly Dictionary<Type, IPackageHandler> _handlers = new();

    public static void Register(IPackageHandler handler)
    {
        _handlers[handler.PackageType] = handler;
    }

    public static void Register<TPackage>(IPackageHandler<TPackage> handler) where TPackage : IPackage
    {
        Register((IPackageHandler)handler);
    }

    public static void Unregister(Type packageType)
    {
        _handlers.Remove(packageType);
    }

    public static bool TryHandle(IPackage package, PackageReceiveContext context)
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

    public static void Handle(IPackage package, PackageReceiveContext context)
    {
        if (!TryHandle(package, context))
        {
            Log.Information($"未注册Package处理器:{package.GetType().Name}");
        }
    }
}
