namespace Game.NetWork.Packages;

public class ConnectionRejectPackage : IPackage
{
    public string Reason = string.Empty;

    public byte ID => (byte)PackageType.ConnectionReject;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.NotConnected;

    public ConnectionRejectPackage()
    {
    }

    public ConnectionRejectPackage(string r)
    {
        Reason = r;
    }


    public void Handle(ProjectNet? projectNet, NetNode netNode, bool isServer)
    {
        ModFileService.Utils.HandleModDataValidationMessage(Reason);
        netNode.Stop($"[连接拒绝]{Reason}");
    }

    public void ReadData(PackageStreamReader reader)
    {
        Reason = reader.ReadString();
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Reason);
    }
}
