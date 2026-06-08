using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class MovingBlockPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemMovingBlocks = project.FindSubsystem<SubsystemMovingBlocks>(true)!;
        switch (Type)
        {
            case EventType.Add:
                if (AddData == null)
                {
                    break;
                }

                var m = subsystemMovingBlocks.LoadAndAddMovingItem(AddData) as SubsystemMovingBlocks.MovingBlockSet;
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
                var mm = Type.HasFlag(EventType.HagTag)
                    ? subsystemMovingBlocks.FindMovingBlocks(MovingBlockId, Position)
                    : subsystemMovingBlocks.FindMovingBlocks(MovingBlockId, null);
                if (mm == null)
                {
                    break;
                }

                if (Type.HasFlag(EventType.Stopped) && mm is SubsystemMovingBlocks.MovingBlockSet blockSet)
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

public sealed class MovingBlockPackageHandler : PackageHandlerBase<MovingBlockPackage>
{
    public override void Handle(MovingBlockPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(MovingBlockPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
