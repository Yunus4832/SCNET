using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class PickablePackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemPickable = project.FindSubsystem<SubsystemPickables>(true)!;
        switch (Type)
        {
            case PickType.Create:
                var tmp = subsystemPickable.Pickables.Find(p => p.Id == Id);
                if (tmp != null)
                {
                    tmp.Value = Value;
                    tmp.Count = Count;
                    tmp.Velocity = Velocity;
                    tmp.StuckMatrix = StuckMatrix;
                }
                else
                {
                    subsystemPickable.CreatePickable(Id, Value, Count, Position, Velocity, StuckMatrix);
                }

                break;
            case PickType.Update:
                foreach (var c in Pickables)
                {
                    subsystemPickable.PickableAction(c.Id, pick => { pick.Position = c.Position; });
                }

                foreach (var c in subsystemPickable.Pickables)
                {
                    if (Pickables.Find(x => x.Id == c.Id) == null)
                    {
                        subsystemPickable.PickablesToRemove.Add(c);
                    }
                }

                break;
            case PickType.Delete:
                subsystemPickable.PickableAction(
                    Id,
                    pick =>
                    {
                        if (PlaySound)
                        {
                            subsystemPickable.PlayPickableCollectedSound(pick);
                        }

                        subsystemPickable.RemovePickable(pick);
                    },
                    false
                );
                break;
            case PickType.RequestSync:
                var flag = subsystemPickable.PickableAction(
                    Id,
                    pick => { netNode.QueuePackage(new PickablePackage(pick, PickType.Create) { To = From }); }
                );
                if (!flag)
                {
                    netNode.QueuePackage(new PickablePackage(Id) { To = From });
                }

                break;
            case PickType.SetFlyToPosition:
                subsystemPickable.PickableAction(Id, pick => { pick.FlyToPosition = FlyToPosition; });
                break;
            case PickType.SyncList:
            case PickType.CreateList:
                if (isServer)
                {
                    break;
                }

                foreach (var pickable in Pickables)
                {
                    subsystemPickable.CreatePickable(pickable.Id, pickable.Value, pickable.Count, pickable.Position,
                        pickable.Velocity, pickable.StuckMatrix);
                }

                break;
            case PickType.DeleteList:
                if (isServer)
                {
                    break;
                }

                foreach (var pickable in Pickables)
                {
                    subsystemPickable.PickableAction(pickable.Id,
                        pick => { subsystemPickable.RemovePickable(pick); }, false);
                }

                break;
        }
    }
}

public sealed class PickablePackageHandler : PackageHandlerBase<PickablePackage>
{
    public override void Handle(PickablePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(PickablePackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
