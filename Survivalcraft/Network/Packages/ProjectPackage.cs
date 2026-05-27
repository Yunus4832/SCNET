using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

/// <summary>
/// 基础包模板复制
/// </summary>
public class ProjectPackage : IPackage
{
    private readonly bool _hasTexture;

    private byte[] _projectData = [];

    private byte[] _textureData = [];

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
        _textureData = textureData ?? [];
        _hasTexture = _textureData.Length > 0;
        _projectData = projectData;
    }


    public void Handle(NetNode netNode, bool isServer)
    {
        var loadingScreen = ScreensManager.FindScreen<GameLoadingScreen>("GameLoading", true)!;
        loadingScreen.ReplyCall(_hasTexture, _textureData, _projectData);
    }

    public void ReadData(PackageStreamReader reader)
    {
        if (reader.ReadBoolean())
        {
            _textureData = reader.ReadBytes(reader.ReadInt32());
        }

        _projectData = reader.ReadBytes(reader.ReadInt32());
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_hasTexture);
        if (_hasTexture)
        {
            writer.Write(_textureData.Length);
            writer.Write(_textureData);
        }

        writer.Write(_projectData.Length);
        writer.Write(_projectData);
    }
}
