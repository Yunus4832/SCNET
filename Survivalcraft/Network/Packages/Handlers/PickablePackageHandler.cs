namespace Game.Network.Packages.Handlers;

public sealed class PickablePackageHandler : PackageHandlerBase<PickablePackage>
{
    internal static void ApplyBaseline(Pickable pickable, PickablePackage package)
    {
        pickable.Value = package.Value;
        pickable.Count = package.Count;
        pickable.Position = package.Position;
        pickable.Velocity = package.Velocity;
        pickable.StuckMatrix = package.StuckMatrix;
        pickable.LastStateTick = package.StateTick;
    }

    public override void Handle(PickablePackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(PickablePackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        // 客户端上行只允许恢复请求，所有拾取物状态均由服务端下发。
        if (isServer != (package.Type == PickablePackage.PickType.RequestSync))
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
                    ApplyBaseline(tmp, package);
                }
                else
                {
                    var created = subsystemPickable.CreatePickable(package.Id, package.Value, package.Count,
                        package.Position, package.Velocity, package.StuckMatrix);
                    if (created is not null)
                    {
                        created.LastStateTick = package.StateTick;
                    }
                }

                break;
            case PickablePackage.PickType.Update:
                subsystemPickable.ApplyPositionSnapshot(package.Pickables, package.StateTick);
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
                if (context.Sender is null)
                {
                    break;
                }

                var interest = project.FindSubsystem<SubsystemNetworkInterest>(true)!;
                if (subsystemPickable.TryGetPickable(package.Id, out var requested) &&
                    interest.IsPositionRelevant(context.Sender, requested.Position.XZ))
                {
                    netNode.QueuePackage(
                        new PickablePackage(requested, PickablePackage.PickType.Create),
                        PackageAudience.To(context.Sender));
                    interest.Entities.AddObserved(
                        context.Sender,
                        EntityInterestGroup.Pickables,
                        requested.Id);
                }
                else
                {
                    netNode.QueuePackage(
                        new PickablePackage(package.Id),
                        PackageAudience.To(context.Sender));
                }

                break;
            case PickablePackage.PickType.SetFlyToPosition:
                subsystemPickable.PickableAction(package.Id, pick => { pick.FlyToPosition = package.FlyToPosition; });
                break;
            case PickablePackage.PickType.CreateList:
                if (isServer)
                {
                    break;
                }

                foreach (var pickable in package.Pickables)
                {
                    var created = subsystemPickable.CreatePickable(pickable.Id, pickable.Value, pickable.Count,
                        pickable.Position, pickable.Velocity, pickable.StuckMatrix);
                    if (created is not null)
                    {
                        created.LastStateTick = package.StateTick;
                    }
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
