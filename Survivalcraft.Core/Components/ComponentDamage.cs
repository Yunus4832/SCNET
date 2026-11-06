using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentDamage : Component, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private ComponentBody _componentBody = null!;

    private ComponentOnFire? _componentOnFire;

    private float _fallResilience;

    private float _fireResilience;

    private bool _destructionNotified;

    public event Action? Destroyed;

    public event Action? Attacked;

    public float HitPoints { get; set; }

    public float AttackResilience { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority) { return; }

        var position = _componentBody.Position;
        if (HitPoints <= 0f)
        {
            HitPoints = 0f;
            NetworkSender.SendToObservers(Entity, EntityStatePackageBuilder.Capture(this, destroyed: true));
            NotifyDestroyed();
            if (WorldRuntimeContext.IsProjectActive(Project) && IsAddedToProject)
            {
                Project.RemoveEntity(Entity, true);
            }

            return;
        }

        var num = MathUtils.Abs(_componentBody.CollisionVelocityChange.Y);
        if (num > _fallResilience)
        {
            var amount = MathUtils.Sqr(MathUtils.Max(num - _fallResilience, 0f)) / 15f;
            Damage(amount);
        }

        if (position.Y is < -10f or > 276f)
        {
            Damage(HitPoints);
        }

        if (_componentOnFire != null && (_componentOnFire.IsOnFire || _componentOnFire.TouchesFire))
        {
            Damage(dt / _fireResilience);
        }
    }

    public void ApplyDamageState(float hitPoints, bool destroyed, bool isAttack)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || WorldRuntimeContext.For(Project).IsAuthority) { return; }

        HitPoints = hitPoints;
        if (isAttack) { Attacked?.Invoke(); }

        if (destroyed) { NotifyDestroyed(); }
    }

    private void NotifyDestroyed()
    {
        if (_destructionNotified) { return; }

        _destructionNotified = true;
        Destroyed?.Invoke();
    }

    public void Damage(float amount, bool isAttack = false)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        var changed = amount > 0f && HitPoints > 0f;
        if (changed) { HitPoints = MathUtils.Max(HitPoints - amount, 0f); }

        NetworkSender.SendToObservers(Entity, EntityStatePackageBuilder.Capture(this, isAttack: isAttack && changed));
        if (isAttack && changed) { Attacked?.Invoke(); }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _componentBody = Entity.FindComponent<ComponentBody>(true)!;
        _componentOnFire = Entity.FindComponent<ComponentOnFire>();
        HitPoints = valuesDictionary.GetValue<float>("HitPoints");
        AttackResilience = valuesDictionary.GetValue<float>("AttackResilience");
        _fallResilience = valuesDictionary.GetValue<float>("FallResilience");
        _fireResilience = valuesDictionary.GetValue<float>("FireResilience");
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("HitPoints", HitPoints);
    }
}
