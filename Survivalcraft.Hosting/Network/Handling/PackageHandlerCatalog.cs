namespace Game.Network.Handling;

/// <summary>由端点的组合管理者显式选择的不可变处理器工厂。</summary>
public sealed class PackageHandlerCatalog
{
    public static PackageHandlerCatalog Empty { get; } = new(new Dictionary<Type, PackageHandlerRegistration>());

    private readonly IReadOnlyDictionary<Type, PackageHandlerRegistration> _registrations;

    internal PackageHandlerCatalog(Dictionary<Type, PackageHandlerRegistration> registrations)
    {
        _registrations =
            new System.Collections.ObjectModel.ReadOnlyDictionary<Type, PackageHandlerRegistration>(registrations);
    }

    public bool Contains(Type type) => _registrations.ContainsKey(type);

    internal bool TryResolve(IProtocolMessage message, out PackageHandlerRegistration? registration) =>
        _registrations.TryGetValue(message.GetType(), out registration);

    internal PackageHandlerRegistration ResolveFamily(Type family)
    {
        return _registrations.Values.FirstOrDefault(registration => registration.PackageType == family) ??
               throw new InvalidOperationException($"No handler is registered for family {family}.");
    }
}
