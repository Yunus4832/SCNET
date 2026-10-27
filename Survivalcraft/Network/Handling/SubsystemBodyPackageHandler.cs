using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class SubsystemBodyPackageHandler : PackageHandlerBase<SubsystemBodyPackage>
{
    internal static bool OwnsCollisionSource(Client? sender, Client? owner) =>
        sender is not null && ReferenceEquals(sender, owner);

    public override void Handle(SubsystemBodyPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(SubsystemBodyPackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        // 身体快照和冲量只由服务端下发，客户端仅报告自身碰撞。
        if (!AcceptsDirection(package.PackageEventType, isServer))
        {
            return;
        }

        var project = GameManager.Project;
        switch (package)
        {
            case BodyStatesPackage message1:
            {
                var bodies = project.FindSubsystem<SubsystemBodies>(true)!;
                var ml = new List<int>();
                // 服务器的动物列表
                foreach (var item in message1.BodyList)
                {
                    bodies.FindBodyByCreatureID(
                        item.CreatureId,
                        body =>
                        {
                            // 应用层最新优先：同一实体的旧轮次快照到达时直接丢弃。
                            if (!IsNewer(message1.StateTick, body.LastBodyStateTick))
                            {
                                return;
                            }

                            body.LastBodyStateTick = message1.StateTick;
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
                    NetworkSender.SendToServer(new EntitySyncRequestPackage { EntityIdList = ml });
                }

                break;
            }
            case BodyCollisionPackage message2:
            {
                project.FindSubsystem<SubsystemBodies>(true)!.FindBodyByCreatureID(
                    message2.CreatureId,
                    from =>
                    {
                        if (isServer && !OwnsCollisionSource(context.Sender, from.Character?.CharacterData.Client))
                        {
                            return;
                        }

                        project.FindSubsystem<SubsystemBodies>(true)!.FindBodyByCreatureID(
                            message2.TargetCreatureId,
                            target =>
                            {
                                if (isServer &&
                                    !project.FindSubsystem<SubsystemNetworkInterest>(true)!
                                        .IsPositionRelevant(context.Sender!, target.Position.XZ))
                                {
                                    return;
                                }

                                target.Velocity = message2.Impulse;
                                target.NetVelocity.SetNext(message2.Impulse);
                                from.CollidedWithBody?.Invoke(target);
                                target.CollidedWithBody?.Invoke(from);
                            }
                        );
                    }
                );
                break;
            }
            case BodyImpulsePackage message3:
            {
                project.FindSubsystem<SubsystemBodies>(true)!
                    .FindBodyByCreatureID(message3.CreatureId, body => { body.ApplyImpulseNet(message3.Impulse); });
                break;
            }
        }
    }

    internal static bool AcceptsDirection(SubsystemBodyPackage.EventType type, bool isServer)
    {
        return !isServer || type == SubsystemBodyPackage.EventType.HandleAxisCollision;
    }

    /// <summary>无符号回绕安全的大小比较：value 是否比 previous 更新。</summary>
    internal static bool IsNewer(uint value, uint previous)
    {
        return unchecked((int)(value - previous)) > 0;
    }
}
