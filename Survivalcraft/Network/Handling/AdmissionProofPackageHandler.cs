using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class AdmissionProofPackageHandler : PackageHandlerBase<AdmissionProofPackage>
{
    public override void Handle(AdmissionProofPackage package, PackageReceiveContext context)
    {
        if (context.IsServer && context.Sender is { } sender)
        {
            context.Node?.AdmissionController?.Submit(sender, package);
        }
    }
}
