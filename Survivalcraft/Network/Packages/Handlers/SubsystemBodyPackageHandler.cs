namespace Game.Network.Packages.Handlers;

public sealed class SubsystemBodyPackageHandler : PackageHandlerBase<SubsystemBodyPackage>
{
    public override void Handle(SubsystemBodyPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(SubsystemBodyPackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (package.PackageEventType)
        {
            case SubsystemBodyPackage.EventType.BodyUpdate:
                var bodies = project.FindSubsystem<SubsystemBodies>(true)!;
                var ml = new List<int>();
                var rl = new List<ComponentBody>();
                // 服务器的动物列表
                foreach (var item in package.BodyList)
                {
                    bodies.FindBodyByCreatureID(
                        item.CreatureId,
                        body =>
                        {
                            if (item.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.PositionChange))
                            {
                                body.NetPosition.SetNext(item.Position);
                            }

                            if (item.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.RotationChange))
                            {
                                body.NetRotation.SetNext(item.Rotation);
                            }

                            if (item.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.VelocityChange))
                            {
                                body.NetVelocity.SetNext(item.Velocity);
                            }

                            if (body.Locomotion == null)
                            {
                                return;
                            }

                            if (item.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.LookAnglesChange))
                            {
                                body.Locomotion.NetLookAngles.SetNext(item.LookAngles);
                            }

                            if (item.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.FlyOrderChange))
                            {
                                body.Locomotion.LastFlyOrder = item.FlyOrder;
                            }
                        },
                        () =>
                        {
                            // 本地没有这个动物，向服务器请求
                            ml.Add(item.CreatureId);
                        }
                    );
                }

                foreach (var item2 in bodies.Bodies)
                {
                    SubsystemBodyPackage.BodyItem?
                        m = package.BodyList.Find(x => x.CreatureId == item2.Entity.EntityId);
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
            case SubsystemBodyPackage.EventType.HandleAxisCollision:
                project.FindSubsystem<SubsystemBodies>(true)!.FindBodyByCreatureID(package.CreatureId, from =>
                {
                    project.FindSubsystem<SubsystemBodies>(true)!.FindBodyByCreatureID(package.TargetCreatureId,
                        target =>
                        {
                            target.Velocity = package.Impulse;
                            target.NetVelocity.SetNext(package.Impulse);
                            from.CollidedWithBody?.Invoke(target);
                            target.CollidedWithBody?.Invoke(from);
                        });
                });
                break;
            case SubsystemBodyPackage.EventType.ApplyImpulse:
                project.FindSubsystem<SubsystemBodies>(true)!
                    .FindBodyByCreatureID(package.CreatureId, body => { body.ApplyImpulseNet(package.Impulse); });
                break;
        }
    }
}
