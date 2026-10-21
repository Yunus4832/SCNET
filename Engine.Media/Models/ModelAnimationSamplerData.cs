namespace Engine.Media.Models;

public sealed class ModelAnimationSamplerData : ModelElementData
{
    public int InputAccessorIndex;

    public int OutputAccessorIndex;

    public ModelAnimationInterpolation Interpolation = ModelAnimationInterpolation.Linear;
}
