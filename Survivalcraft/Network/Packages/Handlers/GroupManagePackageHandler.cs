namespace Game.Network.Packages.Handlers;

public sealed class GroupManagePackageHandler : PackageHandlerBase<GroupManagePackage>
{
    public override void Handle(GroupManagePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(GroupManagePackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;

        switch (package.Command)
        {
            case GroupManagePackage.CommandType.CreateGroup:
                GroupManagePackage.CreateGroup(isServer, subsystemPlayers, netNode, package.FromPlayer,
                    package.GroupName);
                break;
            case GroupManagePackage.CommandType.RequestJoinGroup:
                if (subsystemPlayers.ServerGroups.TryGetValue(package.GroupKey.ToString(), out _))
                {
                    var fromPlayerData = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == package.FromPlayer);
                    var toPlayerData = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == package.ToPlayer);
                    if (fromPlayerData != null && toPlayerData is { IsMainPlayer: true })
                    {
                        DialogsManager.Confirm(
                            $"{fromPlayerData.Name}申请加入你的队伍，是否同意?",
                            btn =>
                            {
                                if (btn != MessageDialogButton.Button1)
                                {
                                    return;
                                }

                                //同意
                                if (isServer)
                                {
                                    GroupManagePackage.JoinGroup(isServer, subsystemPlayers, netNode,
                                        fromPlayerData.PlayerGUID,
                                        package.GroupKey);
                                }
                                else
                                {
                                    toPlayerData.GameWidget.NetPanelWidget?.RefreshView();
                                    netNode.QueuePackage(new GroupManagePackage(package.GroupKey,
                                        fromPlayerData.PlayerGUID, true));
                                }
                            },
                            toPlayerData.GameWidget.GuiWidget
                        );
                    }

                    if (isServer)
                    {
                        package.Except = package.From;
                        netNode.QueuePackage(package);
                    }
                }

                break;
            case GroupManagePackage.CommandType.InviteJoinGroup:
                if (subsystemPlayers.ServerGroups.TryGetValue(package.GroupKey.ToString(), out var fromGroup))
                {
                    var fromPlayerData = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == package.FromPlayer);
                    var toPlayerData = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == package.ToPlayer);
                    if (fromPlayerData != null && toPlayerData is { IsMainPlayer: true })
                    {
                        DialogsManager.Confirm(
                            $"{fromPlayerData.Name}想邀请你加入队伍{fromGroup.Name}，是否同意?",
                            btn =>
                            {
                                if (btn != MessageDialogButton.Button1)
                                {
                                    return;
                                }

                                // 同意
                                if (isServer)
                                {
                                    GroupManagePackage.JoinGroup(isServer, subsystemPlayers, netNode, package.ToPlayer,
                                        package.GroupKey);
                                }
                                else
                                {
                                    toPlayerData.GameWidget.NetPanelWidget?.RefreshView();
                                    netNode.QueuePackage(new GroupManagePackage(package.GroupKey, package.ToPlayer,
                                        true));
                                }
                            },
                            toPlayerData.GameWidget.GuiWidget
                        );
                    }

                    if (isServer)
                    {
                        package.Except = package.From;
                        netNode.QueuePackage(package);
                    }
                }

                break;
            case GroupManagePackage.CommandType.JoinGroup:
                GroupManagePackage.JoinGroup(isServer, subsystemPlayers, netNode, package.FromPlayer, package.GroupKey);
                break;
            case GroupManagePackage.CommandType.ExitGroup:
                GroupManagePackage.ExitGroup(isServer, subsystemPlayers, netNode, package.FromPlayer, package.GroupKey);
                break;
            case GroupManagePackage.CommandType.RenameGroup:
                break;
        }
    }
}
