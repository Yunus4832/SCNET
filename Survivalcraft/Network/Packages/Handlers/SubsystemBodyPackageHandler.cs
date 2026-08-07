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
                // 服务器的动物列表
                foreach (var item in package.BodyList)
                {
                    bodies.FindBodyByCreatureID(
                        item.CreatureId,
                        body =>
                        {
                            // 应用层最新优先：同一实体的旧轮次快照到达时直接丢弃。
                            if (!IsNewer(package.StateTick, body.LastBodyStateTick))
                            {
                                return;
                            }

                            body.LastBodyStateTick = package.StateTick;
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

                if (ml.Count > 0)
                {
                    netNode.QueuePackage(new EntityPackage(ml));
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

    /// <summary>无符号回绕安全的大小比较：value 是否比 previous 更新。</summary>
    private static bool IsNewer(uint value, uint previous)
    {
        return unchecked((int)(value - previous)) > 0;
    }
}
