using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class SubsystemBodyPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (PackageEventType)
        {
            case EventType.BodyUpdate:
                var bodies = project.FindSubsystem<SubsystemBodies>(true)!;
                var ml = new List<int>();
                var rl = new List<ComponentBody>();
                //服务器的动物列表
                foreach (var item in BodyList)
                {
                    bodies.FindBodyByCreatureID(
                        item.CreatureId,
                        body =>
                        {
                            if (item.ChangeFlag.HasFlag(ChangeFlag.PositionChange))
                            {
                                body.NetPosition.SetNext(item.Position);
                            }

                            if (item.ChangeFlag.HasFlag(ChangeFlag.RotationChange))
                            {
                                body.NetRotation.SetNext(item.Rotation);
                            }

                            if (item.ChangeFlag.HasFlag(ChangeFlag.VelocityChange))
                            {
                                body.NetVelocity.SetNext(item.Velocity);
                            }

                            if (body.Locomotion == null)
                            {
                                return;
                            }

                            if (item.ChangeFlag.HasFlag(ChangeFlag.LookAnglesChange))
                            {
                                body.Locomotion.NetLookAngles.SetNext(item.LookAngles);
                            }

                            if (item.ChangeFlag.HasFlag(ChangeFlag.FlyOrderChange))
                            {
                                body.Locomotion.LastFlyOrder = item.FlyOrder;
                            }
                        },
                        () =>
                        {
                            //本地没有这个动物，向服务器请求
                            ml.Add(item.CreatureId);
                        }
                    );
                }

                foreach (var item2 in bodies.Bodies)
                {
                    BodyItem? m = BodyList.Find(x => x.CreatureId == item2.Entity.EntityId);
                    if (!m.HasValue)
                    {
                        rl.Add(item2);
                    }
                }

                if (ml.Count > 0)
                {
                    netNode.QueuePackage(new EntityPackage(ml));
                }

                if (rl.Count > 0)
                {
                    foreach (var b in rl)
                    {
                        project.RemoveEntity(b.Entity, true);
                    }
                }

                break;
            case EventType.HandleAxisCollision:
                project.FindSubsystem<SubsystemBodies>(true)!.FindBodyByCreatureID(CreatureId, from =>
                {
                    project.FindSubsystem<SubsystemBodies>(true)!.FindBodyByCreatureID(TargetCreatureId, target =>
                    {
                        target.Velocity = Impulse;
                        target.NetVelocity.SetNext(Impulse);
                        from.CollidedWithBody?.Invoke(target);
                        target.CollidedWithBody?.Invoke(from);
                    });
                });
                break;
            case EventType.ApplyImpulse:
                project.FindSubsystem<SubsystemBodies>(true)!
                    .FindBodyByCreatureID(CreatureId, body => { body.ApplyImpulseNet(Impulse); });
                break;
        }
    }
}

public sealed class SubsystemBodyPackageHandler : PackageHandlerBase<SubsystemBodyPackage>
{
    public override void Handle(SubsystemBodyPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(SubsystemBodyPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
