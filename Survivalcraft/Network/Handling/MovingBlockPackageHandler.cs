using Game.Network.Transport;

namespace Game.Network.Handling;

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
        if (package is PistonSoundPackage message1)
        {
            project.FindSubsystem<SubsystemAudio>(true)!.PlaySound("Audio/Piston", 1f, 0f, message1.Position, 2f, true);
            return;
        }

        var subsystemMovingBlocks = project.FindSubsystem<SubsystemMovingBlocks>(true)!;
        switch (package)
        {
            case MovingBlockAddedPackage message4:
            {
                if (message4.AddData == null ||
                    subsystemMovingBlocks.FindMovingBlocks(message4.NetworkId) is not null)
                {
                    break;
                }

                var baseline = new EntitySystem.TemplatesDatabase.ValuesDictionary();
                baseline.ApplyOverridesUseMessagePack(message4.AddData);
                if (subsystemMovingBlocks.LoadAndAddMovingItem(baseline) is SubsystemMovingBlocks.MovingBlockSet m)
                {
                    m.NetworkId = message4.NetworkId;
                    m.LastNetworkStateTick = message4.StateTick;
                    subsystemMovingBlocks.MovingBlockSets.Add(m);
                }

                break;
            }
            default:
                var mm = subsystemMovingBlocks.FindMovingBlocks(package.NetworkId);
                if (mm == null)
                {
                    break;
                }

                if (package is MovingBlockStatePackage message3)
                {
                    if (!mm.NetworkStopped && unchecked((int)(message3.StateTick - mm.LastNetworkStateTick)) > 0)
                    {
                        mm.LastNetworkStateTick = message3.StateTick;
                        mm.Position = message3.Position;
                        mm.CurrentVelocity = message3.Velocity;
                        mm.Speed = message3.Speed;
                    }
                }
                else if (package is MovingBlockStoppedPackage message2)
                {
                    mm.NetworkStopped = true;
                    mm.Position = message2.Position;
                    mm.CurrentVelocity = message2.Velocity;
                    mm.Speed = message2.Speed;
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
