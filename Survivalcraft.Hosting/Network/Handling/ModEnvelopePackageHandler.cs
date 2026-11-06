namespace Game.Network.Handling;

public sealed class ModEnvelopePackageHandler(ModNetworkHooks network) : PackageHandlerBase<ModEnvelopePackage>
{
    public override void Handle(ModEnvelopePackage package, PackageReceiveContext context)
    {
        network.Dispatch(package, context);
    }
}
