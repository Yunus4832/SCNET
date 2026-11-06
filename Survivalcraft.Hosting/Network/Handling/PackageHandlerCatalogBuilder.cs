namespace Game.Network.Handling;

public sealed class PackageHandlerCatalogBuilder
{
    private readonly Dictionary<Type, PackageHandlerRegistration> _registrations = [];

    public void RegisterFamily<TPackage>(Func<IPackageHandler<TPackage>> factory) where TPackage : IProtocolMessage
    {
        var types = ProtocolMessageCodec.MessageTypes.Where(typeof(TPackage).IsAssignableFrom).ToArray();
        if (types.Length == 0) { throw new ArgumentException("A handler family must match known protocol messages."); }

        Register(factory, types);
    }

    public void RegisterLogical<TPackage>(Func<IPackageHandler<TPackage>> factory) where TPackage : IProtocolMessage =>
        Register(factory, [typeof(TPackage)]);

    private void Register<TPackage>(Func<IPackageHandler<TPackage>> factory, Type[] types)
        where TPackage : IProtocolMessage
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (types.Any(_registrations.ContainsKey))
        {
            throw new InvalidOperationException("A message already has a registered handler.");
        }

        var registration = new PackageHandlerRegistration(typeof(TPackage), () => factory());
        foreach (var type in types) { _registrations.Add(type, registration); }
    }

    public PackageHandlerCatalog Build() => new(new Dictionary<Type, PackageHandlerRegistration>(_registrations));
}
