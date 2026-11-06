namespace Game.Network.Handling;

public sealed class AdmissionProofPackageHandler : PackageHandlerBase<AdmissionProofPackage>
{
    public override void Handle(AdmissionProofPackage package, PackageReceiveContext context)
    {
        if (context is { IsServer: true, Sender: { } sender })
        {
            context.Node?.AdmissionController?.Submit(sender, package);
        }
    }
}
