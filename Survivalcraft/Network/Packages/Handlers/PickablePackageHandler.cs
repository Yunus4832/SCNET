namespace Game.Network.Packages.Handlers;

public sealed class PickablePackageHandler : PackageHandlerBase<PickablePackage>
{
    public override void Handle(PickablePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(PickablePackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemPickable = project.FindSubsystem<SubsystemPickables>(true)!;
        switch (package.Type)
        {
            case PickablePackage.PickType.Create:
                if (subsystemPickable.TryGetPickable(package.Id, out var tmp))
                {
                    tmp.Value = package.Value;
                    tmp.Count = package.Count;
                    tmp.Velocity = package.Velocity;
                    tmp.StuckMatrix = package.StuckMatrix;
                }
                else
                {
                    subsystemPickable.CreatePickable(package.Id, package.Value, package.Count, package.Position,
                        package.Velocity, package.StuckMatrix);
                }

                break;
            case PickablePackage.PickType.Update:
                var receivedIds = new HashSet<ushort>();
                foreach (var c in package.Pickables)
                {
                    receivedIds.Add(c.Id);
                    subsystemPickable.PickableAction(c.Id, pick => { pick.Position = c.Position; });
                }

                foreach (var c in subsystemPickable.Pickables)
                {
                    if (!receivedIds.Contains(c.Id))
                    {
                        subsystemPickable.PickablesToRemove.Add(c);
                    }
                }

                break;
            case PickablePackage.PickType.Delete:
                subsystemPickable.PickableAction(
                    package.Id,
                    pick =>
                    {
                        if (package.PlaySound)
                        {
                            subsystemPickable.PlayPickableCollectedSound(pick);
                        }

                        subsystemPickable.RemovePickable(pick);
                    },
                    false
                );
                break;
            case PickablePackage.PickType.RequestSync:
                var flag = subsystemPickable.PickableAction(
                    package.Id,
                    pick =>
                    {
                        netNode.QueuePackage(new PickablePackage(pick, PickablePackage.PickType.Create)
                        { To = package.From });
                    }
                );
                if (!flag)
                {
                    netNode.QueuePackage(new PickablePackage(package.Id) { To = package.From });
                }

                break;
            case PickablePackage.PickType.SetFlyToPosition:
                subsystemPickable.PickableAction(package.Id, pick => { pick.FlyToPosition = package.FlyToPosition; });
                break;
            case PickablePackage.PickType.SyncList:
            case PickablePackage.PickType.CreateList:
                if (isServer)
                {
                    break;
                }

                foreach (var pickable in package.Pickables)
                {
                    subsystemPickable.CreatePickable(pickable.Id, pickable.Value, pickable.Count, pickable.Position,
                        pickable.Velocity, pickable.StuckMatrix);
                }

                break;
            case PickablePackage.PickType.DeleteList:
                if (isServer)
                {
                    break;
                }

                foreach (var pickable in package.Pickables)
                {
                    subsystemPickable.PickableAction(pickable.Id,
                        pick => { subsystemPickable.RemovePickable(pick); }, false);
                }

                break;
        }
    }
}
