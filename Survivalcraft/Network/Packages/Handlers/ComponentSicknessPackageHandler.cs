namespace Game.Network.Packages.Handlers;

public sealed class ComponentSicknessPackageHandler : PackageHandlerBase<ComponentSicknessPackage>
{
    public override void Handle(ComponentSicknessPackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        project.FindEntityById(package.EntityId, entity =>
        {
            var sickness = entity.FindComponent<ComponentSickness>();
            if (sickness == null)
            {
                return;
            }

            sickness.SicknessDuration = package.SicknessDuration;
            if (package.SicknessDuration > 0f)
            {
                sickness.NauseaEffect();
            }
        });
    }
}
