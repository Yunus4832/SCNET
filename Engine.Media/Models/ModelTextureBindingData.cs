namespace Engine.Media.Models;

public sealed class ModelTextureBindingData : ModelElementData
{
    public int TextureIndex;

    public int TextureCoordinate;

    /// <summary>Normal scale or occlusion strength, depending on the material channel.</summary>
    public float Scale = 1;
}
