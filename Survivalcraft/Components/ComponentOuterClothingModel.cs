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
        foreach (var node in Model.Nodes)
        {
            var modelNode = _componentHumanModel.Model.FindNode(node.Name)!;
            SetNodeTransform(node.Index, _componentHumanModel.GetNodeTransform(modelNode.Index));
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
        if (DrawNodeIndices.Length != 4)
        {
            throw new InvalidOperationException("Invalid number of meshes in OuterClothing model.");
        }

        DrawNodeIndices[0] = model.FindNode("Leg1")!.Index;
        DrawNodeIndices[1] = model.FindNode("Leg2")!.Index;
        DrawNodeIndices[2] = model.FindNode("Body")!.Index;
        DrawNodeIndices[3] = model.FindNode("Head")!.Index;
    }
}
