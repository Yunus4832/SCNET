using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentGlowingEyes : Component, IDrawable
{
    private static readonly int[] _drawOrders = new int[1];

    private ComponentCreatureModel _componentCreatureModel = null!;

    private readonly GlowPoint[] _eyeGlowPoints = new GlowPoint[2];

    private SubsystemGlow _subsystemGlow = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private Vector3 GlowingEyesOffset { get; set; }

    private Color GlowingEyesColor { get; set; }

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
        if ((GlowPoint?)_eyeGlowPoints[0] == null ||
            !_componentCreatureModel.IsVisibleForCamera)
        {
            return;
        }

        _eyeGlowPoints[0]!.Color = Color.Transparent;
        _eyeGlowPoints[1]!.Color = Color.Transparent;
        var modelBone = _componentCreatureModel.Model.FindBone("Head", false);
        if (modelBone == null)
        {
            return;
        }

        var matrix = _componentCreatureModel.AbsoluteBoneTransformsForCamera[modelBone.Index];
        matrix *= camera.InvertedViewMatrix;
        var vector = Vector3.Normalize(matrix.Up);
        var num = Vector3.Dot(matrix.Translation - camera.ViewPosition, camera.ViewDirection);
        if (!(num > 0f))
        {
            return;
        }

        var translation = matrix.Translation;
        var cellLight = _subsystemTerrain.Terrain.GetCellLight(Terrain.ToCell(translation.X),
            Terrain.ToCell(translation.Y), Terrain.ToCell(translation.Z));
        var num2 = LightingManager.LightIntensityByLightValue[cellLight];
        float num3 = 0f - Vector3.Dot(vector, Vector3.Normalize(matrix.Translation - camera.ViewPosition)) > 0.7f
            ? 1
            : 0;
        num3 *= MathUtils.Saturate(1f * (num - 1f));
        num3 *= MathUtils.Saturate((1f - num2 - 0.5f) / 0.5f);
        if (!(num3 > 0.25f))
        {
            return;
        }

        var vector2 = Vector3.Normalize(matrix.Right);
        var vector3 = -Vector3.Normalize(matrix.Forward);
        var color = GlowingEyesColor * num3;
        _eyeGlowPoints[0].Position = translation + vector2 * GlowingEyesOffset.X +
                                     vector3 * GlowingEyesOffset.Y + vector * GlowingEyesOffset.Z;
        _eyeGlowPoints[0].Right = vector2;
        _eyeGlowPoints[0].Up = vector3;
        _eyeGlowPoints[0].Forward = vector;
        _eyeGlowPoints[0].Size = 0.01f;
        _eyeGlowPoints[0].FarSize = 0.06f;
        _eyeGlowPoints[0].FarDistance = 14f;
        _eyeGlowPoints[0].Color = color;
        _eyeGlowPoints[1].Position = translation - vector2 * GlowingEyesOffset.X +
                                     vector3 * GlowingEyesOffset.Y + vector * GlowingEyesOffset.Z;
        _eyeGlowPoints[1].Right = vector2;
        _eyeGlowPoints[1].Up = vector3;
        _eyeGlowPoints[1].Forward = vector;
        _eyeGlowPoints[1].Size = 0.01f;
        _eyeGlowPoints[1].FarSize = 0.06f;
        _eyeGlowPoints[1].FarDistance = 14f;
        _eyeGlowPoints[1].Color = color;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGlow = Project.FindSubsystem<SubsystemGlow>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _componentCreatureModel = Entity.FindComponent<ComponentCreatureModel>(true)!;
        GlowingEyesOffset = valuesDictionary.GetValue<Vector3>("GlowingEyesOffset");
        GlowingEyesColor = valuesDictionary.GetValue<Color>("GlowingEyesColor");
    }

    public override void OnEntityAdded()
    {
        for (var i = 0; i < _eyeGlowPoints.Length; i++)
        {
            _eyeGlowPoints[i] = _subsystemGlow.AddGlowPoint();
        }
    }

    public override void OnEntityRemoved()
    {
        foreach (var point in _eyeGlowPoints)
        {
            _subsystemGlow.RemoveGlowPoint(point);
        }
    }
}
