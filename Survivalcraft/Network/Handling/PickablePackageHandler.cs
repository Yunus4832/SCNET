using Game.Network.Replication;
using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class PickablePackageHandler : PackageHandlerBase<PickablePackage>
{
    internal static void ApplyBaseline(Pickable pickable, PickableAddedPackage package)
    {
        pickable.Value = package.Value;
        pickable.Count = package.Count;
        pickable.Position = package.Position;
        pickable.Velocity = package.Velocity;
        pickable.StuckMatrix = package.StuckMatrix;
        pickable.FlyToPosition = null;
        pickable.NetworkPosition = null;
        pickable.ToRemove = false;
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
        switch (package)
        {
            case PickableAddedPackage message1:
            {
                if (subsystemPickable.TryGetPickable(message1.Id, out var tmp))
                {
                    ApplyBaseline(tmp, message1);
                }
                else
                {
                    var created = subsystemPickable.CreatePickable(
                        message1.Id,
                        message1.Value,
                        message1.Count,
                        message1.Position,
                        message1.Velocity,
                        message1.StuckMatrix
                    );
                    if (created is not null)
                    {
                        created.LastStateTick = message1.StateTick;
                    }
                }

                break;
            }
            case PickablePositionsPackage message2:
            {
                subsystemPickable.ApplyPositionSnapshot(message2.Positions, message2.StateTick);
                break;
            }
            case PickableRemovedPackage message3:
            {
                subsystemPickable.PickableAction(
                    message3.Id,
                    pick =>
                    {
                        if (message3.PlaySound)
                        {
                            subsystemPickable.PlayPickableCollectedSound(pick);
                        }

                        subsystemPickable.RemovePickable(pick);
                    },
                    false
                );
                break;
            }
            case PickableSyncRequestPackage message4:
            {
                if (context.Sender is null)
                {
                    break;
                }

                var interest = project.FindSubsystem<SubsystemNetworkInterest>(true)!;
                if (subsystemPickable.TryGetPickable(message4.Id, out var requested) &&
                    interest.IsPositionRelevant(context.Sender, requested.Position.XZ))
                {
                    netNode.QueuePackage(
                        PickablePackageBuilder.Capture(requested),
                        PackageAudience.To(context.Sender)
                    );
                    interest.Entities.AddObserved(
                        context.Sender,
                        EntityInterestGroup.Pickables,
                        requested.Id
                    );
                }
                else
                {
                    netNode.QueuePackage(
                        new PickableRemovedPackage { Id = message4.Id },
                        PackageAudience.To(context.Sender)
                    );
                }

                break;
            }
            case PickablesAddedPackage message5:
            {
                if (isServer)
                {
                    break;
                }

                foreach (var pickable in message5.Pickables)
                {
                    var created = subsystemPickable.CreatePickable(
                        pickable.Id,
                        pickable.Value,
                        pickable.Count,
                        pickable.Position,
                        pickable.Velocity,
                        pickable.StuckMatrix
                    );
                    if (created is not null)
                    {
                        created.LastStateTick = message5.StateTick;
                    }
                }

                break;
            }
            case PickablesRemovedPackage message6:
            {
                if (isServer)
                {
                    break;
                }

                foreach (var id in message6.RemovedIds)
                {
                    subsystemPickable.PickableAction(
                        id,
                        pick => { subsystemPickable.RemovePickable(pick); },
                        false
                    );
                }

                break;
            }
        }
    }
}
