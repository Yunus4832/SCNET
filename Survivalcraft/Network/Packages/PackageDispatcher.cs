using System.Runtime.ExceptionServices;

namespace Game.Network.Packages;

public static class PackageDispatcher
{
    private sealed class MethodPackageHandler(Type packageType, MethodInfo handleMethod) : IPackageHandler
    {
        public Type PackageType { get; } = packageType;

        public void Handle(IPackage package, NetNode? netNode, bool isServer)
        {
            try
            {
                handleMethod.Invoke(package, [netNode, isServer]);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            }
        }
    }

    private static readonly Dictionary<Type, IPackageHandler> _handlers = new();

    public static void Register(IPackageHandler handler)
    {
        _handlers[handler.PackageType] = handler;
    }

    public static void Register<TPackage>(IPackageHandler<TPackage> handler) where TPackage : IPackage
    {
        Register((IPackageHandler)handler);
    }

    public static void RegisterLegacyHandler(Type packageType)
    {
        if (_handlers.ContainsKey(packageType))
        {
            return;
        }

        var handleMethod = packageType.GetMethod(
            "Handle",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            [typeof(NetNode), typeof(bool)],
            null);

        handleMethod ??= packageType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method =>
            {
                if (method.Name != "Handle")
                {
                    return false;
                }

                var parameters = method.GetParameters();
                return parameters.Length == 2 &&
                       parameters[0].ParameterType == typeof(NetNode) &&
                       parameters[1].ParameterType == typeof(bool);
            });

        if (handleMethod != null)
        {
            Register(new MethodPackageHandler(packageType, handleMethod));
        }
    }

    public static void Unregister(Type packageType)
    {
        _handlers.Remove(packageType);
    }

    public static bool TryHandle(IPackage package, NetNode? netNode, bool isServer)
    {
        var packageType = package.GetType();
        if (_handlers.TryGetValue(packageType, out var handler))
        {
            handler.Handle(package, netNode, isServer);
            return true;
        }

        RegisterLegacyHandler(packageType);
        if (!_handlers.TryGetValue(packageType, out handler))
        {
            return false;
        }

        handler.Handle(package, netNode, isServer);
        return true;
    }

    public static void Handle(IPackage package, NetNode? netNode, bool isServer)
    {
        if (!TryHandle(package, netNode, isServer))
        {
            Log.Information($"未注册Package处理器:{package.GetType().Name}");
        }
    }
}
