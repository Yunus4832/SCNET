using Engine.Graphics;
using Engine.Media.Images;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemBlocksTexture : Subsystem
{
    private bool _hasBlocksTexture;

    public Texture2D BlocksTexture
    {
        get => field is not null ? field : throw new InvalidOperationException("BlockTexture is not initialized");
        set;
    } = null!;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        Display.DeviceReset += DisplayDeviceReset;
        LoadBlocksTexture();
    }

    public override void Dispose()
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            BlocksTexture = null!;
            return;
        }

        Display.DeviceReset -= DisplayDeviceReset;
        DisposeBlocksTexture();
    }

    private void LoadBlocksTexture()
    {
        var subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        DisposeBlocksTexture();
        BlocksTexture = ClientBootstrapContent.For(Project).BlocksTextureBytes is { } data
            ? Texture2D.Load(Image.Load(new MemoryStream(data)))
            : BlocksTexturesManager.LoadTexture(subsystemGameInfo.WorldSettings.BlocksTextureName);
        _hasBlocksTexture = true;
    }

    private void DisposeBlocksTexture()
    {
        if (!_hasBlocksTexture)
        {
            return;
        }

        if (!ContentManager.IsContent(BlocksTexture))
        {
            BlocksTexture.Dispose();
        }

        BlocksTexture = null!;
        _hasBlocksTexture = false;
    }

    private void DisplayDeviceReset()
    {
        LoadBlocksTexture();
    }
}
