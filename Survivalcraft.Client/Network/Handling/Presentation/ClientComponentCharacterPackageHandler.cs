namespace Game.Network.Handling.Presentation;

public sealed class ClientComponentCharacterPackageHandler : ComponentCharacterPackageHandler
{
    protected override void ApplyAuthoritativePosition(ComponentCharacter player, Vector3 position, Vector3 velocity)
    {
        base.ApplyAuthoritativePosition(player, position, velocity);
        if (player.IsLocallyControlled)
        {
            var input = player.Entity.FindComponent<ComponentInput>(true)!;
            input.Navigation.Cancel("teleported");
            input.ViewControl.Cancel("teleported");
        }
    }
}
