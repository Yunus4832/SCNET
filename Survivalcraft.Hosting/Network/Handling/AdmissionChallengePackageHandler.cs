namespace Game.Network.Handling;

public sealed class AdmissionChallengePackageHandler : PackageHandlerBase<AdmissionChallengePackage>
{
    public override void Handle(AdmissionChallengePackage package, PackageReceiveContext context)
    {
        if (context is { IsServer: false, Node: { } node, Sender: { } sender })
        {
            node.ReceiveAdmissionChallenge(sender, package);
        }
    }
}
