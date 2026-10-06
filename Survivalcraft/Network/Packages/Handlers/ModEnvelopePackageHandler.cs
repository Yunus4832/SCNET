namespace Game.Network.Packages.Handlers;

public sealed class ModEnvelopePackageHandler : PackageHandlerBase<ModEnvelopePackage>
{
    public override void Handle(ModEnvelopePackage package, PackageReceiveContext context)
    {
        CurrentModRuntime.Value?.Network.Dispatch(package, context);
    }
}
