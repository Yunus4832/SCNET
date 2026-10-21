using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class PlayerDataPackageHandler : PackageHandlerBase<PlayerDataPackage>
{
    internal static bool CanCreatePlayer(Client? sender, IEnumerable<Guid> existingPlayers) =>
        sender is not null && sender.GUID != Guid.Empty && !existingPlayers.Contains(sender.GUID);

    internal static bool AcceptsDirection(PlayerDataPackage.DataType type, bool isServer) =>
        isServer
            ? type is PlayerDataPackage.DataType.Create or PlayerDataPackage.DataType.SetUpdateLocation
            : type is PlayerDataPackage.DataType.Modify;

    public override void Handle(PlayerDataPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(PlayerDataPackage)}");
            return;
        }

        if (GameManager.Project is null || !AcceptsDirection(package.Type, isServer))
        {
            return;
        }

        var project = GameManager.Project;
        PlayerData? playerData;
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        switch (package.Type)
        {
            case PlayerDataPackage.DataType.Create:
                if (!CanCreatePlayer(
                        context.Sender,
                        subsystemPlayers.PlayersData.Select(player => player.PlayerGUID)
                    ) ||
                    !ProtocolEnum.TryDecode(package.PlayerClass, out PlayerClass requestedClass) ||
                    string.IsNullOrWhiteSpace(package.SkinName))
                {
                    break;
                }

                var name = PlayerData.SanitizeName(package.PlayerName.Trim());
                if (!PlayerData.VerifyName(name) || subsystemPlayers.PlayersData.Any(player => player.Name == name))
                {
                    break;
                }

                playerData = new PlayerData(project)
                {
                    PlayerGUID = context.Sender!.GUID,
                    Name = name,
                    CharacterSkinName = package.SkinName,
                    PlayerClass = requestedClass
                };
                subsystemPlayers.AddPlayerData(playerData);
                netNode.QueuePackage(PlayerPackageBuilder.Capture(subsystemPlayers), PackageAudience.Global);
                break;
            case PlayerDataPackage.DataType.Modify:
                if (!ProtocolEnum.TryDecode(package.PlayerClass, out PlayerClass updatedClass))
                {
                    break;
                }

                var playerData2 = subsystemPlayers.FindPlayerData(p => p.PlayerGUID == package.PlayerGuid);
                if (playerData2 != null)
                {
                    playerData2.Name = package.PlayerName;
                    playerData2.CharacterSkinName = package.SkinName;
                    playerData2.PlayerClass = updatedClass;
                }

                break;
            case PlayerDataPackage.DataType.SetUpdateLocation:
                var player = subsystemPlayers.PlayersData.Find(x => ReferenceEquals(x.Client, context.Sender));
                if (player == null)
                {
                    Log.Warning(
                        $"Ignored terrain update location without matching player: " +
                        $"client={context.Sender?.ID.ToString() ?? "null"}, center={package.UpdateLocation.Center}."
                    );
                    break;
                }

                if (context.Sender is null ||
                    !project.FindSubsystem<SubsystemNetworkInterest>(true)!
                        .SetRequestedLocation(context.Sender, player, package.UpdateLocation))
                {
                    Log.Warning(
                        $"Rejected invalid terrain update location: client={context.Sender?.ID.ToString() ?? "null"}, " +
                        $"center={package.UpdateLocation.Center}."
                    );
                }

                break;
        }
    }
}
