using Game.Network.Transport;

namespace Game.Network.Handling.Presentation;

public sealed class ClientBlockEditPackageHandler : BlockEditPackageHandler
{
    public override void Handle(BlockEditPackage package, PackageReceiveContext context)
    {
        base.Handle(package, context);
        if (!IsAuthorityReply(context) || !context.Runtime.HasPresentation || context.Project is not { } project)
        {
            return;
        }

        switch (package)
        {
            case OpenInventoryPackage inventory:
                project.FindSubsystem<SubsystemBlockInventoryPresentation>(true)!.OpenInventory(inventory.InventoryId);
                break;
            case OpenSignEditorPackage sign:
                if (project.FindSubsystem<SubsystemCharacters>()?.LocalCharacter is { } character)
                {
                    project.FindSubsystem<SubsystemSignBlockPresentation>(true)!.OpenEditor(character, sign.Point3);
                }

                break;
        }
    }
}
