using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ConnectionRequestPackage : IPackage
{
    public const int VerifyMagic = 9421523; //校验码

    public int Magic;

    public Guid TmpToken;

    public Guid MultiplayerClientId;

    public string Version = string.Empty;

    public string ModDataHash = string.Empty;

    public byte ID => (byte)PackageType.ConnectionRequest;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.NotConnected;

    public ConnectionRequestPackage()
    {
    }

    public ConnectionRequestPackage(
        Guid tmpToken,
        string serverVersion,
        Guid multiplayerClientId,
        string modDataHash
    )
    {
        Magic = VerifyMagic;
        TmpToken = tmpToken;
        MultiplayerClientId = multiplayerClientId;
        Version = serverVersion;
        ModDataHash = modDataHash;
    }


    public void ReadData(PackageStreamReader reader)
    {
        Magic = reader.ReadInt32();
        Version = reader.ReadString();
        TmpToken = reader.ReadGuid();
        MultiplayerClientId = reader.ReadGuid();
        ModDataHash = reader.ReadString();
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(TmpToken);
        writer.Write(MultiplayerClientId);
        writer.Write(ModDataHash);
    }

}
