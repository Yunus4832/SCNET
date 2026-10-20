using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class ConnectionRequestPackage : IPackage
{
    public const int VerifyMagic = 9421523; //校验码

    public int Magic;

    public Guid TmpToken;

    public Guid MultiplayerClientId;

    public int ProtocolVersion;

    public string ModDataHash = string.Empty;

    public byte ID => (byte)PackageType.ConnectionRequest;




    public ClientState MinNeedState => ClientState.NotConnected;

    public ConnectionRequestPackage()
    {
    }

    public ConnectionRequestPackage(
        Guid tmpToken,
        int protocolVersion,
        Guid multiplayerClientId,
        string modDataHash
    )
    {
        Magic = VerifyMagic;
        TmpToken = tmpToken;
        MultiplayerClientId = multiplayerClientId;
        ProtocolVersion = protocolVersion;
        ModDataHash = modDataHash;
    }


    public void ReadData(PackageStreamReader reader)
    {
        Magic = reader.ReadInt32();
        ProtocolVersion = reader.ReadInt32();
        TmpToken = reader.ReadGuid();
        MultiplayerClientId = reader.ReadGuid();
        ModDataHash = reader.ReadString();
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Magic);
        writer.Write(ProtocolVersion);
        writer.Write(TmpToken);
        writer.Write(MultiplayerClientId);
        writer.Write(ModDataHash);
    }
}
