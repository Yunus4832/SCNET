using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class GroupManagePackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;

        switch (Command)
        {
            case CommandType.CreateGroup:
                CreateGroup(isServer, subsystemPlayers, netNode, FromPlayer, GroupName);
                break;
            case CommandType.RequestJoinGroup:
                if (subsystemPlayers.ServerGroups.TryGetValue(GroupKey.ToString(), out _))
                {
                    var fromPlayerData = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == FromPlayer);
                    var toPlayerData = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == ToPlayer);
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
                                    JoinGroup(isServer, subsystemPlayers, netNode, fromPlayerData.PlayerGUID,
                                        GroupKey);
                                }
                                else
                                {
                                    toPlayerData.GameWidget.NetPanelWidget?.RefreshView();
                                    netNode.QueuePackage(new GroupManagePackage(GroupKey,
                                        fromPlayerData.PlayerGUID, true));
                                }
                            },
                            toPlayerData.GameWidget.GuiWidget
                        );
                    }

                    if (isServer)
                    {
                        Except = From;
                        netNode.QueuePackage(this);
                    }
                }

                break;
            case CommandType.InviteJoinGroup:
                if (subsystemPlayers.ServerGroups.TryGetValue(GroupKey.ToString(), out var fromGroup))
                {
                    var fromPlayerData = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == FromPlayer);
                    var toPlayerData = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == ToPlayer);
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

                                //同意
                                if (isServer)
                                {
                                    JoinGroup(isServer, subsystemPlayers, netNode, ToPlayer, GroupKey);
                                }
                                else
                                {
                                    toPlayerData.GameWidget.NetPanelWidget?.RefreshView();
                                    netNode.QueuePackage(new GroupManagePackage(GroupKey, ToPlayer, true));
                                }
                            },
                            toPlayerData.GameWidget.GuiWidget
                        );
                    }

                    if (isServer)
                    {
                        Except = From;
                        netNode.QueuePackage(this);
                    }
                }

                break;
            case CommandType.JoinGroup:
                JoinGroup(isServer, subsystemPlayers, netNode, FromPlayer, GroupKey);
                break;
            case CommandType.ExitGroup:
                ExitGroup(isServer, subsystemPlayers, netNode, FromPlayer, GroupKey);
                break;
            case CommandType.RenameGroup:
                break;
        }
    }
}

public sealed class GroupManagePackageHandler : PackageHandlerBase<GroupManagePackage>
{
    public override void Handle(GroupManagePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(GroupManagePackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
