using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

/// <summary>
/// 基础包模板复制
/// </summary>
public partial class SignBlockPackage : IPackage
{
    public Point3 Point;

    public SignData? SignData;

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
        Point = point;
        SignData = new SignData
        {
            Lines = lines,
            Colors = colors,
            Url = url
        };
    }


    public void ReadData(PackageStreamReader reader)
    {
        Point = reader.ReadPoint3();
        SignData = new SignData
        {
            Url = reader.ReadString(),
            Colors = new Color[reader.ReadByte()]
        };
        for (byte i = 0; i < SignData.Colors.Length; i++)
        {
            SignData.Colors[i] = reader.ReadColor();
        }

        SignData.Lines = new string[reader.ReadByte()];
        for (byte i = 0; i < SignData.Colors.Length; i++)
        {
            SignData.Lines[i] = reader.ReadString();
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Point);
        if (SignData == null)
        {
            return;
        }

        writer.Write(SignData.Url);
        writer.Write((byte)SignData.Colors.Length);
        foreach (var i in SignData.Colors)
        {
            writer.Write(i);
        }

        writer.Write((byte)SignData.Lines.Length);
        foreach (var i in SignData.Lines)
        {
            writer.Write(i);
        }
    }
}
