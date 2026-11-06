using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class
    NewYearCelebrationAnnouncementPackageHandler : PackageHandlerBase<NewYearCelebrationAnnouncementPackage>
{
    public override void Handle(NewYearCelebrationAnnouncementPackage package, PackageReceiveContext context)
    {
        if (context.IsServer ||
            context.Project is not { } project ||
            !context.Runtime.HasPresentation ||
            !WorldRuntimeContext.IsProjectActive(project) ||
            package.CharacterId == Guid.Empty)
        {
            return;
        }

        var data = project.FindSubsystem<SubsystemCharacters>(true)!.FindCharacterData(character =>
            character.CharacterId == package.CharacterId
        );
        if (data?.ComponentCharacter is { IsLocallyControlled: true } local)
        {
            local.Entity.FindComponent<ComponentGui>(true)!.DisplayLargeMessage(
                "Happy New Year!",
                "--- Enjoy the fireworks ---",
                15f,
                3f
            );
        }
    }
}
