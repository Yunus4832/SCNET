using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public sealed class ComponentBoatPresentation : Component, IUpdateable
{
    private ComponentBody _body = null!;

    private ComponentDamage _damage = null!;

    private SubsystemAudio _audio = null!;

    private float _lastHitPoints;

    private bool _inactive;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.CreatureModels;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _body = Entity.FindComponent<ComponentBody>(true)!;
        _damage = Entity.FindComponent<ComponentDamage>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _lastHitPoints = _damage.HitPoints;
    }

    public void Update(float dt)
    {
        if (_inactive ||
            !IsAddedToProject ||
            !WorldRuntimeContext.IsProjectActive(Project) ||
            !WorldRuntimeContext.For(Project).HasPresentation) { return; }

        var hitPoints = _damage.HitPoints;
        if (((_lastHitPoints >= .66f && hitPoints < .66f) || (_lastHitPoints >= .33f && hitPoints < .33f)) &&
            _body.ImmersionFactor > 0f)
        {
            _audio.PlaySound("Audio/Sinking", 1f, 0f, _body.Position, 4f, true);
        }

        _lastHitPoints = hitPoints;
    }

    public override void OnEntityAdded()
    {
        _inactive = false;
        if (_damage is not null) { _lastHitPoints = _damage.HitPoints; }
    }

    public override void OnEntityRemoved() => _inactive = true;
    public override void Dispose() => _inactive = true;
}
