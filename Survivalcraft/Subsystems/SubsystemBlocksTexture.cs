using Engine.Graphics;
using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemBlocksTexture : Subsystem
{
    public Texture2D BlocksTexture
    {
        get => field is not null ? field : throw new InvalidOperationException("BlockTexture is not initialized");
        set;
    } = null!;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        Display.DeviceReset += DisplayDeviceReset;
        LoadBlocksTexture();
    }

    public override void Dispose()
    {
        Display.DeviceReset -= DisplayDeviceReset;
        DisposeBlocksTexture();
    }

    private void LoadBlocksTexture()
    {
        var subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        BlocksTexture = BlocksTexturesManager.LoadTexture(subsystemGameInfo.WorldSettings.BlocksTextureName);
    }

    private void DisposeBlocksTexture()
    {
        if (ContentManager.IsContent(BlocksTexture))
        {
            return;
        }

        BlocksTexture.Dispose();
        BlocksTexture = null!;
    }

    private void DisplayDeviceReset()
    {
        LoadBlocksTexture();
    }
}
