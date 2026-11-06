using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentIntroShip : Component, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    private ComponentFrame _componentFrame = null!;

    private ComponentModel _componentModel = null!;

    private double _creationTime;

    private SubsystemSkyPresentation _subsystemSky = null!;

    private SubsystemTime _subsystemTime = null!;

    private SubsystemGameWidgets _subsystemViews = null!;

    public float Heading { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!IsAddedToProject ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation) { return; }

        var s = 3.5f * MathUtils.Saturate(0.07f * ((float)(_subsystemTime.GameTime - _creationTime) - 6f));
        var matrix = _componentFrame.Matrix;
        var vector = Quaternion.CreateFromRotationMatrix(matrix).ToYawPitchRoll();
        vector.X = Heading;
        vector.Y = 0.05f * MathUtils.Sin((float)MathUtils.NormalizeAngle(0.77 * _subsystemTime.GameTime + 1.0));
        vector.Z = 0.12f * MathUtils.Sin((float)MathUtils.NormalizeAngle(1.12 * _subsystemTime.GameTime + 2.0));
        matrix = Matrix.CreateFromYawPitchRoll(vector.X, vector.Y, vector.Z) *
                 Matrix.CreateTranslation(matrix.Translation);
        matrix.Translation += s * matrix.Forward * new Vector3(1f, 0f, 1f) * dt;
        _componentFrame.Position = matrix.Translation;
        _componentFrame.Rotation = Quaternion.CreateFromRotationMatrix(matrix);
        if (_componentModel?.Model != null)
        {
            _componentModel.Pose.WorldTransform = matrix;
        }

        if (_subsystemTime.GameTime - _creationTime > 10.0 &&
            _subsystemViews.CalculateDistanceFromNearestView(matrix.Translation) >
            _subsystemSky.VisibilityRange + 30f)
        {
            Project.RemoveEntity(Entity, true);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSkyPresentation>(true)!;
        _componentFrame = Entity.FindComponent<ComponentFrame>(true)!;
        _componentModel = Entity.FindComponent<ComponentModel>(true)!;
        _creationTime = _subsystemTime.GameTime;
        Heading = valuesDictionary.GetValue<float>("Heading");
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("Heading", Heading);
    }
}
