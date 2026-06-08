namespace Game.Network.Packages.Handlers;

public sealed class MovingBlockPackageHandler : PackageHandlerBase<MovingBlockPackage>
{
    public override void Handle(MovingBlockPackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemMovingBlocks = project.FindSubsystem<SubsystemMovingBlocks>(true)!;
        switch (package.Type)
        {
            case MovingBlockPackage.EventType.Add:
                if (package.AddData == null)
                {
                    break;
                }

                var m =
                    subsystemMovingBlocks.LoadAndAddMovingItem(package.AddData) as SubsystemMovingBlocks.MovingBlockSet;
                var subsystemAudio = project.FindSubsystem<SubsystemAudio>(true)!;
                if (m != null)
                {
                    subsystemMovingBlocks.MovingBlockSets.Add(m);
                    if (m.Id == SubsystemPistonBlockBehavior.IdString)
                    {
                        subsystemAudio.PlaySound("Audio/Piston", 1f, 0f, m.Position, 2f, true);
                    }
                }

                break;
            default:
                var mm = package.Type.HasFlag(MovingBlockPackage.EventType.HagTag)
                    ? subsystemMovingBlocks.FindMovingBlocks(package.MovingBlockId, package.Position)
                    : subsystemMovingBlocks.FindMovingBlocks(package.MovingBlockId, null);
                if (mm == null)
                {
                    break;
                }

                if (package.Type.HasFlag(MovingBlockPackage.EventType.Stopped) &&
                    mm is SubsystemMovingBlocks.MovingBlockSet blockSet)
                {
                    subsystemMovingBlocks.DoStop(blockSet);
                }
                else
                {
                    subsystemMovingBlocks.RemoveMovingBlockSetLogic(mm);
                }

                break;
        }
    }
}
