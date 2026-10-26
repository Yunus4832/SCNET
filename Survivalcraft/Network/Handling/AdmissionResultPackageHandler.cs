using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class AdmissionResultPackageHandler : PackageHandlerBase<AdmissionResultPackage>
{
    public override void Handle(AdmissionResultPackage package, PackageReceiveContext context)
    {
        if (!context.IsServer && context.Node is { } node && context.Sender is { } sender)
        {
            node.ReceiveAdmissionResult(sender, package);
        }
    }
}
