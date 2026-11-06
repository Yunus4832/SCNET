namespace Game.Network.Handling;

internal sealed class PackageHandlerRegistration(Type packageType, Func<IPackageHandler> factory)
{
    public Type PackageType { get; } = packageType;

    public IPackageHandler CreateHandler()
    {
        var handler = factory();
        if (handler is null || handler.PackageType != PackageType)
        {
            throw new InvalidOperationException("The handler factory must create its registered message handler.");
        }

        return handler;
    }
}
