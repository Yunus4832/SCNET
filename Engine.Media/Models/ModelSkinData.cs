using Engine.Core.Mathematics;

namespace Engine.Media.Models;

public sealed class ModelSkinData : ModelElementData
{
    public int SkeletonNodeIndex = -1;

    public int[] JointNodes = [];

    public Matrix[] InverseBindMatrices = [];
}
