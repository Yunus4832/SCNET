namespace Game.Network.Packages.Handlers;

public sealed class MovingBlockPackageHandler : PackageHandlerBase<MovingBlockPackage>
{
    public override void Handle(MovingBlockPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (isServer || GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        if (package.Type == MovingBlockPackage.EventType.PistonSound)
        {
            project.FindSubsystem<SubsystemAudio>(true)!.PlaySound(
                "Audio/Piston", 1f, 0f, package.Position, 2f, true);
            return;
        }

        var subsystemMovingBlocks = project.FindSubsystem<SubsystemMovingBlocks>(true)!;
        switch (package.Type)
        {
            case MovingBlockPackage.EventType.Add:
                if (package.AddData == null ||
                    subsystemMovingBlocks.FindMovingBlocks(package.NetworkId) is not null)
                {
                    break;
                }

                var m =
                    subsystemMovingBlocks.LoadAndAddMovingItem(package.AddData) as SubsystemMovingBlocks.MovingBlockSet;
                if (m != null)
                {
                    m.NetworkId = package.NetworkId;
                    m.LastNetworkStateTick = package.StateTick;
                    subsystemMovingBlocks.MovingBlockSets.Add(m);
                }

                break;
            default:
                var mm = subsystemMovingBlocks.FindMovingBlocks(package.NetworkId);
                if (mm == null)
                {
                    break;
                }

                if (package.Type == MovingBlockPackage.EventType.Update)
                {
                    if (!mm.NetworkStopped && unchecked((int)(package.StateTick - mm.LastNetworkStateTick)) > 0)
                    {
                        mm.LastNetworkStateTick = package.StateTick;
                        mm.Position = package.Position;
                        mm.CurrentVelocity = package.Velocity;
                        mm.Speed = package.Speed;
                    }
                }
                else if (package.Type == MovingBlockPackage.EventType.Stopped)
                {
                    mm.NetworkStopped = true;
                    mm.Position = package.Position;
                    mm.CurrentVelocity = package.Velocity;
                    mm.Speed = package.Speed;
                    subsystemMovingBlocks.DoStop(mm);
                }
                else
                {
                    subsystemMovingBlocks.RemoveMovingBlockSetLogic(mm);
                }

                break;
        }
    }
}
