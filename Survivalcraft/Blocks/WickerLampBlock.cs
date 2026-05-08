namespace Game.Blocks;

public class WickerLampBlock : AlphaTestCubeBlock
{
    public const int Index = 17;

    public override bool FurnitureBuilt { get; set; } = true;

    public override int GetFaceTextureSlot(int face, int value)
    {
        return face != 5 ? TextureSlot : 4;
    }
}
