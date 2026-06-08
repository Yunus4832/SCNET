using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

/// <summary>
/// 基础包模板复制
/// </summary>
public partial class ProjectPackage : IPackage
{
    public readonly bool HasTexture;

    public byte[] ProjectData = [];

    public byte[] TextureData = [];

    public byte ID => (byte)PackageType.Project;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.NotConnected;

    public ProjectPackage()
    {
    }

    public ProjectPackage(byte[]? textureData, byte[] projectData)
    {
        TextureData = textureData ?? [];
        HasTexture = TextureData.Length > 0;
        ProjectData = projectData;
    }


    public void ReadData(PackageStreamReader reader)
    {
        if (reader.ReadBoolean())
        {
            TextureData = reader.ReadBytes(reader.ReadInt32());
        }

        ProjectData = reader.ReadBytes(reader.ReadInt32());
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(HasTexture);
        if (HasTexture)
        {
            writer.Write(TextureData.Length);
            writer.Write(TextureData);
        }

        writer.Write(ProjectData.Length);
        writer.Write(ProjectData);
    }
}
