using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentCreatureModel : ComponentModel, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public ComponentCreature ComponentCreature = null!;

    protected ComponentCreaturePose creaturePose = null!;

    private float _injuryColorFactor;

    protected readonly Random random = new();

    protected SubsystemGameInfo subsystemGameInfo = null!;

    protected SubsystemTime subsystemTime = null!;

    public float Bob { get; set; }

    public float MovementAnimationPhase { get; set; }

    public float DeathPhase { get; set; }

    public Vector3 DeathCauseOffset { get; set; }

    public Vector3 VisualEyePosition => creaturePose.EyePosition +
                                        (creaturePose.Kind == CreaturePoseKind.Human
                                            ? 2 * Bob * ComponentCreature.ComponentBody.Matrix.Up
                                            : Vector3.Zero);

    public UpdateOrder UpdateOrder => creaturePose.UpdateOrder + 1;

    public virtual void Update(float dt)
    {
        if (ComponentCreature.ComponentHealth.Health == 0) { DeathPhase = MathUtils.Min(DeathPhase + 3 * dt, 1); }

        if (ComponentCreature.ComponentHealth.HealthChange < 0) { _injuryColorFactor = 1; }

        _injuryColorFactor = MathUtils.Saturate(_injuryColorFactor - 3 * dt);
    }

    public override void Animate()
    {
        Opacity = ComponentCreature.ComponentSpawn.SpawnDuration > 0f
            ? (float)MathUtils.Saturate(
                (subsystemGameInfo.TotalElapsedGameTime - ComponentCreature.ComponentSpawn.SpawnTime) /
                ComponentCreature.ComponentSpawn.SpawnDuration
            )
            : 1f;
        if (ComponentCreature.ComponentSpawn.DespawnTime.HasValue)
        {
            Opacity = MathUtils.Min(
                Opacity.Value,
                (float)MathUtils.Saturate(
                    1.0 -
                    (subsystemGameInfo.TotalElapsedGameTime -
                     ComponentCreature.ComponentSpawn.DespawnTime.Value) /
                    ComponentCreature.ComponentSpawn.DespawnDuration
                )
            );
        }

        DiffuseColor = Vector3.Lerp(Vector3.One, new Vector3(1f, 0f, 0f), _injuryColorFactor);
        if (Opacity.HasValue && Opacity.Value < 1f)
        {
            var num = ComponentCreature.ComponentBody.ImmersionFactor >= 1f;
            var flag = subsystemSky.ViewUnderWaterDepth > 0f;
            RenderingMode = num == flag
                ? ModelRenderingMode.TransparentAfterWater
                : ModelRenderingMode.TransparentBeforeWater;
        }
        else
        {
            RenderingMode = ModelRenderingMode.Solid;
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        base.Load(valuesDictionary, idToEntityMap);
        subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        subsystemSky = Project.FindSubsystem<SubsystemSkyPresentation>(true)!;
        subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        ComponentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        creaturePose = Entity.FindComponent<ComponentCreaturePose>(true)!;
        ComponentCreature.ComponentHealth.Attacked += OnAttacked;
    }

    private void OnAttacked(ComponentCreature attacker)
    {
        if (DeathPhase == 0 && ComponentCreature.ComponentHealth.Health == 0)
        {
            DeathCauseOffset = attacker.ComponentBody.BoundingBox.Center() -
                               ComponentCreature.ComponentBody.BoundingBox.Center();
        }
    }

    public override void Dispose()
    {
        if (ComponentCreature is not null) { ComponentCreature.ComponentHealth.Attacked -= OnAttacked; }

        base.Dispose();
    }
}
