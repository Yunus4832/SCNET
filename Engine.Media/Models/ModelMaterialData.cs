using Engine.Core.Mathematics;

namespace Engine.Media.Models;

public sealed class ModelMaterialData : ModelElementData
{
    public Vector4 BaseColorFactor = Vector4.One;

    public float MetallicFactor = 1;

    public float RoughnessFactor = 1;

    public Vector3 EmissiveFactor;

    public ModelTextureBindingData? BaseColorTexture;

    public ModelTextureBindingData? MetallicRoughnessTexture;

    public ModelTextureBindingData? NormalTexture;

    public ModelTextureBindingData? OcclusionTexture;

    public ModelTextureBindingData? EmissiveTexture;

    public string AlphaMode = "OPAQUE";

    public float AlphaCutoff = 0.5f;

    public bool DoubleSided;
}
