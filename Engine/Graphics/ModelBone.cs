using Engine.Core;

namespace Engine.Graphics;

public class ModelBone
{
    public readonly List<ModelBone> ChildBones = [];

    public Matrix Transform;

    public required Model Model { get; set; }

    public int Index { get; set; }

    public required string Name { get; set; }

    public ModelBone? ParentBone { get; set; }

    public ReadOnlyList<ModelBone> ReadOnlyChildBones => new(ChildBones);
}
