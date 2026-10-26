using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class AdmissionChallengePackageHandler : PackageHandlerBase<AdmissionChallengePackage>
{
    public override void Handle(AdmissionChallengePackage package, PackageReceiveContext context)
    {
        if (!context.IsServer && context.Node is { } node && context.Sender is { } sender)
        {
            node.ReceiveAdmissionChallenge(sender, package);
        }
    }
}
