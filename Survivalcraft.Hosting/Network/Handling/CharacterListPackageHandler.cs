namespace Game.Network.Handling;

public sealed class CharacterListPackageHandler : PackageHandlerBase<CharacterListPackage>
{
    public override void Handle(CharacterListPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (isServer || context.Project is null)
        {
            return;
        }

        context.Project
            .FindSubsystem<SubsystemCharacters>(true)!
            .ApplyCharacterList(package.Characters);
    }
}
