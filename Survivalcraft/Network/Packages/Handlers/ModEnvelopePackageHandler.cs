namespace Game.Network.Packages.Handlers;

public sealed class ModEnvelopePackageHandler : PackageHandlerBase<ModEnvelopePackage>
{
    public override void Handle(ModEnvelopePackage package, NetNode? netNode, bool isServer)
    {
        CurrentModRuntime.Value?.Network.Dispatch(package, netNode, isServer);
    }
}
