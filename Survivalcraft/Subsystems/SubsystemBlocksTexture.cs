using Engine.Graphics;

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
#if !SERVER
        Display.DeviceReset += DisplayDeviceReset;
        LoadBlocksTexture();
#endif
    }

    public override void Dispose()
    {
#if !SERVER
        Display.DeviceReset -= DisplayDeviceReset;
        DisposeBlocksTexture();
#endif
    }

    private void LoadBlocksTexture()
    {
        var subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        BlocksTexture = BlocksTexturesManager.LoadTexture(subsystemGameInfo.WorldSettings.BlocksTextureName);
        _hasBlocksTexture = true;
    }

    private void DisposeBlocksTexture()
    {
        if (!_hasBlocksTexture)
        {
            return;
        }

        if (ContentManager.IsContent(BlocksTexture))
        {
            return;
        }

        BlocksTexture.Dispose();
        BlocksTexture = null!;
        _hasBlocksTexture = false;
    }

    private void DisplayDeviceReset()
    {
        LoadBlocksTexture();
    }
}
