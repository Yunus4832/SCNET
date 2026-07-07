using Game.Network.Enums;
using Game.Network.Serialization;
namespace Game.Network.Packages;

public class ConnectionRequestPackage : IPackage
{
    public const int VerifyMagic = 9421523; //校验码

    public string CommunityAccountId = string.Empty;

    public int Magic;

    public string Password = string.Empty;

    public Guid TmpToken;

    public string Token = string.Empty;

    public string User = string.Empty;

    public string Version = string.Empty;

    public string ModDataHash = string.Empty;

    public string Nickname = string.Empty;

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
        string user,
        string token,
        string passwd,
        string modDataHash
    )
    {
        Magic = VerifyMagic;
        TmpToken = tmpToken;
        User = user;
        Token = token;
        Version = serverVersion;
        Password = passwd;
        ModDataHash = modDataHash;
    }


    public void ReadData(PackageStreamReader reader)
    {
        Magic = reader.ReadInt32();
        Version = reader.ReadString();
        Password = reader.ReadString();
        User = reader.ReadString();
        TmpToken = reader.ReadGuid();
        Token = reader.ReadString();
        ModDataHash = reader.ReadString();
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(Password);
        writer.Write(User);
        writer.Write(TmpToken);
        writer.Write(Token);
        writer.Write(ModDataHash);
    }

}
