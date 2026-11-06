namespace Game.Network.Handling;

public sealed class AdmissionResultPackageHandler : PackageHandlerBase<AdmissionResultPackage>
{
    public override void Handle(AdmissionResultPackage package, PackageReceiveContext context)
    {
        if (context is { IsServer: false, Node: { } node, Sender: { } sender })
        {
            node.ReceiveAdmissionResult(sender, package);
        }
    }
}
