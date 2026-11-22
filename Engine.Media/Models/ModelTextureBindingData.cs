namespace Engine.Media.Models;

public sealed class ModelTextureBindingData : ModelElementData
{
    public int TextureIndex;

    public int TextureCoordinate;

    /// <summary>按材质通道表示法线缩放系数或遮蔽强度。</summary>
    public float Scale = 1;
}
