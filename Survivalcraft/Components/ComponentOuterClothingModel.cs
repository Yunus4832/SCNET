using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentOuterClothingModel : ComponentModel
{
    private ComponentCreature _componentCreature = null!;

    private ComponentHumanModel _componentHumanModel = null!;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _componentHumanModel = Entity.FindComponent<ComponentHumanModel>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
    }

    public override void Animate()
    {
        Opacity = _componentHumanModel.Opacity;
        foreach (var bone in Model.Bones)
        {
            var modelBone = _componentHumanModel.Model.FindBone(bone.Name)!;
            SetBoneTransform(bone.Index, _componentHumanModel.GetBoneTransform(modelBone.Index));
        }

        if (Opacity is < 1f)
        {
            var num = _componentCreature.ComponentBody.ImmersionFactor >= 1f;
            var flag = subsystemSky.ViewUnderWaterDepth > 0f;
            RenderingMode = num == flag
                ? ModelRenderingMode.TransparentAfterWater
                : ModelRenderingMode.TransparentBeforeWater;
        }
        else
        {
            RenderingMode = ModelRenderingMode.AlphaThreshold;
        }

        base.Animate();
    }

    public override void SetModel(Model model)
    {
        base.SetModel(model);
        if (MeshDrawOrders.Length != 4)
        {
            throw new InvalidOperationException("Invalid number of meshes in OuterClothing model.");
        }

        MeshDrawOrders[0] = model.Meshes.IndexOf(model.FindMesh("Leg1")!);
        MeshDrawOrders[1] = model.Meshes.IndexOf(model.FindMesh("Leg2")!);
        MeshDrawOrders[2] = model.Meshes.IndexOf(model.FindMesh("Body")!);
        MeshDrawOrders[3] = model.Meshes.IndexOf(model.FindMesh("Head")!);
    }
}
