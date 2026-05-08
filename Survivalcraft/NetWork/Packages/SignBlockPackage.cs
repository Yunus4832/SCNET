namespace Game.NetWork.Packages;

/// <summary>
/// 基础包模板复制
/// </summary>
public class SignBlockPackage : IPackage
{
    private Point3 _point;

    private SignData? _signData;

    public byte ID => (byte)PackageType.SignBlock;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;


    public SignBlockPackage()
    {
    }

    public SignBlockPackage(Point3 point, string[] lines, Color[] colors, string url)
    {
        _point = point;
        _signData = new SignData
        {
            Lines = lines,
            Colors = colors,
            Url = url
        };
    }

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        if (_signData != null)
        {
            projectNet.FindSubsystem<SubsystemSignBlockBehavior>(true)!
                .SetSignData(_point, _signData.Lines, _signData.Colors, _signData.Url);
        }

        if (isServer)
        {
            netNode.QueuePackage(this);
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _point = reader.ReadPoint3();
        _signData = new SignData
        {
            Url = reader.ReadString(),
            Colors = new Color[reader.ReadByte()]
        };
        for (byte i = 0; i < _signData.Colors.Length; i++)
        {
            _signData.Colors[i] = reader.ReadColor();
        }

        _signData.Lines = new string[reader.ReadByte()];
        for (byte i = 0; i < _signData.Colors.Length; i++)
        {
            _signData.Lines[i] = reader.ReadString();
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_point);
        if (_signData == null)
        {
            return;
        }

        writer.Write(_signData.Url);
        writer.Write((byte)_signData.Colors.Length);
        foreach (var i in _signData.Colors)
        {
            writer.Write(i);
        }

        writer.Write((byte)_signData.Lines.Length);
        foreach (var i in _signData.Lines)
        {
            writer.Write(i);
        }
    }
}
