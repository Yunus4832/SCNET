using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public sealed class PlayerJoinedPackage : IPackage
{
    public ClientPackage ClientInfo = new();
    public ValuesDictionary PlayerData = new();
    public byte[] EntityData = [];

    public byte ID => (byte)PackageType.PlayerJoined;
    public Client? To { get; set; }
    public Client? Except { get; set; }
    public Client? From { get; set; }
    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public PlayerJoinedPackage()
    {
    }

    public PlayerJoinedPackage(Project project, PlayerData playerData, Entity entity)
    {
        var client = playerData.Client ?? throw new InvalidOperationException("Player client is not connected");
        ClientInfo = new ClientPackage(client.ID, client.TokenId, client.GUID);
        playerData.Save(PlayerData);
        EntityData = InitialWorldSnapshotPackage.SerializeEntities(project, [entity]);
    }

    public void WriteData(PackageStreamWriter writer)
    {
        ClientInfo.WriteData(writer);
        writer.Write(PlayerData);
        writer.WriteBuff(EntityData);
    }

    public void ReadData(PackageStreamReader reader)
    {
        ClientInfo = new ClientPackage();
        ClientInfo.ReadData(reader);
        PlayerData = reader.ReadValuesDictionary();
        EntityData = reader.ReadBuff();
    }
}
