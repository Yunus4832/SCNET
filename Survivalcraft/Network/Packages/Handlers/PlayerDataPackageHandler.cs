using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class PlayerDataPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        PlayerData? playerData;
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        switch (Type)
        {
            case DataType.Create:
                playerData = new PlayerData(project);
                if (Vd != null)
                {
                    playerData.Load(Vd);
                }

                subsystemPlayers.AddPlayerData(playerData);
                playerData.Name = PlayerData.CreateNewName(playerData.Name);
                //服务器广播给所有客户端，添加玩家
                netNode.QueuePackage(new PlayerDataPackage(playerData, DataType.AddPlayer));
                break;
            case DataType.Modify:
                var playerData2 = subsystemPlayers.FindPlayerData(p => p.PlayerGUID == PlayerGuid);
                if (playerData2 != null)
                {
                    var client = From;
                    var name = PlayerData.SanitizeName(PlayerName);
                    if (client != null && !string.IsNullOrEmpty(client.Nickname))
                    {
                        playerData2.Name = client.Nickname;
                    }
                    else if (!PlayerData.IsDuplicateName(name))
                    {
                        playerData2.Name = name;
                    }

                    playerData2.CharacterSkinName = SkinName;
                    playerData2.PlayerClass = PlayerClass;
                    if (isServer)
                    {
                        netNode.QueuePackage(this);
                    }
                }

                break;
            case DataType.Delete:
                netNode.RemoveClient(From);
                break;
            case DataType.AddPlayer:
                //客户端接收到添加玩家广播
                playerData = new PlayerData(project);
                if (Vd != null)
                {
                    playerData.Load(Vd);
                }

                subsystemPlayers.AddPlayerData(playerData);
                netNode.QueuePackage(new PlayerDataPackage(playerData, DataType.ClientKnownPlayer));
                break;
            case DataType.SetUpdateLocation:
                var player = subsystemPlayers.PlayersData.Find(x => x.Client == From);
                if (player != null)
                {
                    var updater = project.FindSubsystem<SubsystemTerrain>(true)!.TerrainUpdater;
                    updater.SetLastChunksUpdateCenter(player.PlayerIndex, UpdateLocation.LastChunksUpdateCenter);
                    updater.SetUpdateLocation(player.PlayerIndex, UpdateLocation.Center,
                        UpdateLocation.VisibilityDistance, UpdateLocation.ContentDistance);
                }

                break;
            case DataType.CloseTime:
                var p3 = project.FindSubsystem<SubsystemPlayers>(true)!.MainPlayer;
                if (p3 != null)
                {
                    p3.ComponentGui.CloseTime = Visibility;
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(
                            "服务器关闭提醒", PlayerName,
                            LanguageControl.Yes,
                            LanguageControl.No,
                            _ => { DialogsManager.HideAllDialogs(); }
                        )
                    );
                }

                break;
            case DataType.Bugle:
                var mainPlayer = project.FindSubsystem<SubsystemPlayers>(true)!.MainPlayer;
                if (mainPlayer == null)
                {
                    break;
                }

                if (PlayerGuid == Guid.Empty ||
                    (PlayerGuid != Guid.Empty &&
                     mainPlayer.PlayerData.PlayerGUID == PlayerGuid))
                {
                    DialogsManager.HideAllDialogs();
                    mainPlayer.ComponentHealth.IsInvulnerable = true;
                    BugleContent = BugleContent.Replace("[n]", "\n").Replace("[e]", " ");
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(
                            BugleTitle,
                            BugleContent,
                            LanguageControl.Ok,
                            string.Empty,
                            _ =>
                            {
                                DialogsManager.HideAllDialogs();
                                mainPlayer.ComponentHealth.IsInvulnerable = false;
                            }
                        )
                    );
                }

                break;
            case DataType.Count:
                var mainPlayer2 = project.FindSubsystem<SubsystemPlayers>(true)!.MainPlayer;
                if (mainPlayer2 == null)
                {
                    break;
                }

                var clientPlayerCount = project.FindSubsystem<SubsystemPlayers>(true)!.PlayersData.Count;
                Log.Information($"隐身测试，服务端人数：{PlayerCount}; 客户端人数：{clientPlayerCount}");
                if (clientPlayerCount != PlayerCount)
                {
                    ScreensManager.SwitchScreen("NetPlay");
                    GameManager.DisposeProject();
                    CommonLib.Net.Stop();
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(
                            "连接异常",
                            "检测到玩家人数异常，请重新连接服务器",
                            LanguageControl.Ok
                        )
                    );
                }

                break;
            case DataType.AddNoMsg:
                project.FindSubsystem<SubsystemPlayers>(true)!.NoMsgPlayerGuidList.Add(PlayerGuid.ToString());
                break;
            case DataType.RemoveNoMsg:
                project.FindSubsystem<SubsystemPlayers>(true)!.NoMsgPlayerGuidList.Remove(PlayerGuid.ToString());
                break;
        }
    }
}

public sealed class PlayerDataPackageHandler : PackageHandlerBase<PlayerDataPackage>
{
    public override void Handle(PlayerDataPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(PlayerDataPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
