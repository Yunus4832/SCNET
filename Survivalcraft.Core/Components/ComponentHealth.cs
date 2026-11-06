using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentHealth : Component, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public const string Name = "ComponentHealth";

    private ComponentCreature _componentCreature = null!;

    private ComponentOnFire _componentOnFire = null!;

    private ComponentCharacter? _componentCharacter;

    private float _lastHealth;

    private readonly Random _random = new();

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private SubsystemTimeOfDay _subsystemTimeOfDay = null!;

    private bool _wasStanding;

    public LocalizedText CauseOfDeath { get; set; } = LocalizedText.Empty;

    public bool IsInvulnerable { get; set; }

    public float Health { get; set; }

    public float HealthChange { get; set; }

    public BreathingMode BreathingMode { get; set; }

    public float Air { get; set; }

    public float AirCapacity { get; set; }

    public bool CanStrand { get; set; }

    public float AttackResilience { get; set; }

    public float FallResilience { get; set; }

    public float FireResilience { get; set; }

    public double? DeathTime { get; set; }

    public float CorpseDuration { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        var position = _componentCreature.ComponentBody.Position;
        if (Health is > 0f and < 1f)
        {
            if (WorldRuntimeContext.For(Project).IsAuthority && Time.PeriodicEvent(2.0, 0.0))
            {
                NetworkSender.SendToObservers(Entity, EntityStatePackageBuilder.Capture(this));
            }

            var num = 0f;
            if (_componentCharacter != null)
            {
                if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Harmless)
                {
                    num = 0.0166666675f;
                }
                else if (_componentCharacter.ComponentSleep.SleepFactor.CloseTo(1f) &&
                         _componentCharacter.ComponentVitalStats.Food > 0f)
                {
                    num = 0.00166666671f;
                }
                else if (_componentCharacter.ComponentVitalStats.Food > 0.5f)
                {
                    num = 0.00111111114f;
                }
            }
            else
            {
                num = 0.00111111114f;
            }

            Heal(_subsystemGameInfo.TotalElapsedGameTimeDelta * num);
        }

        if (BreathingMode == BreathingMode.Air)
        {
            var cellContents = _subsystemTerrain.Terrain.GetCellContents(
                Terrain.ToCell(position.X),
                Terrain.ToCell(_componentCreature.ComponentCreaturePose.EyePosition.Y),
                Terrain.ToCell(position.Z)
            );
            Air = WorldContentBinding.For(Project).Catalog.Blocks[cellContents] is FluidBlock ||
                  position.Y > 700f
                ? MathUtils.Saturate(Air - dt / AirCapacity)
                : 1f;
        }
        else if (BreathingMode == BreathingMode.Water)
        {
            Air = _componentCreature.ComponentBody.ImmersionFactor > 0.25f
                ? 1f
                : MathUtils.Saturate(Air - dt / AirCapacity);
        }

        if (_componentCreature.ComponentBody is { ImmersionFactor: > 0f, ImmersionFluidBlock: MagmaBlock })
        {
            //岩浆伤害
            var cause = new LocalizedText(Name, "1", "1");
            Injure(2f * _componentCreature.ComponentBody.ImmersionFactor * dt, null, false, cause);
        }

        var num3 = MathUtils.Abs(_componentCreature.ComponentBody.CollisionVelocityChange.Y);
        if (!_wasStanding && num3 > FallResilience)
        {
            //掉落伤害，这里需要客户端计算伤害同步到服务器
            var num4 = MathUtils.Sqr(MathUtils.Max(num3 - FallResilience, 0f)) / 15f;
            if (_componentCharacter != null)
            {
                num4 /= _componentCharacter.ComponentLevel.ResilienceFactor;
            }

            var cause = new LocalizedText(Name, "2", "2");
            ApplyEnvironmentalDamage(num4, null, cause, ComponentHealthPackage.RequestInjureType.Fall);
        }

        _wasStanding = _componentCreature.ComponentBody.StandingOnValue.HasValue ||
                       _componentCreature.ComponentBody.StandingOnBody != null;
        if (position.Y < 0f && _subsystemTime.PeriodicGameTimeEvent(2.0, 0.0))
        {
            //掉出世界伤害
            Injure(0.1f, null, true, new LocalizedText(Name, "3", "3"));
        }

        var num5 = _subsystemTime.PeriodicGameTimeEvent(1.0, 0.0);
        if (num5 && Air == 0f)
        {
            var num6 = 0.12f;
            if (_componentCharacter != null)
            {
                num6 /= _componentCharacter.ComponentLevel.ResilienceFactor;
            }

            //窒息伤害
            Injure(num6, null, false, new LocalizedText(Name, "7", "7"));
        }

        if (num5 && (_componentOnFire.IsOnFire || _componentOnFire.TouchesFire))
        {
            var amount = 1f / FireResilience;
            if (_componentCharacter != null)
            {
                amount /= _componentCharacter.ComponentLevel.ResilienceFactor;
            }

            ApplyEnvironmentalDamage(
                amount,
                _componentOnFire.Attacker,
                new LocalizedText(Name, "5", "5"),
                ComponentHealthPackage.RequestInjureType.Fire
            );
        }

        if (num5 &&
            CanStrand &&
            _componentCreature.ComponentBody.ImmersionFactor < 0.25f &&
            (_componentCreature.ComponentBody.StandingOnValue != 0 ||
             _componentCreature.ComponentBody.StandingOnBody != null))
            //搁浅伤害
        {
            Injure(0.05f, null, false, new LocalizedText(Name, "6", "6"));
        }

        HealthChange = Health - _lastHealth;
        _lastHealth = Health;
        if (Health == 0f && HealthChange < 0f)
        {
            if (WorldRuntimeContext.For(Project).IsAuthority)
            {
                var position3 = (_componentCreature.ComponentBody.BoundingBox.Min +
                                 _componentCreature.ComponentBody.BoundingBox.Max) /
                                2f;
                foreach (var item in Entity.FindComponents<IInventory>())
                {
                    item?.DropAllItems(position3);
                }
            }

            if (WorldRuntimeContext.For(Project).IsAuthority && DeathTime is null)
            {
                DeathTime = _subsystemGameInfo.TotalElapsedGameTime;
                NetworkSender.SendToObservers(Entity, EntityStatePackageBuilder.Capture(this));
            }
        }

        if (WorldRuntimeContext.For(Project).IsAuthority &&
            Health <= 0f &&
            CorpseDuration > 0f &&
            _subsystemGameInfo.TotalElapsedGameTime - DeathTime > CorpseDuration)
        {
            _componentCreature.ComponentSpawn.Despawn();
        }
    }

    internal void ApplyEnvironmentalDamage(
        float amount,
        ComponentCreature? attacker,
        LocalizedText cause,
        ComponentHealthPackage.RequestInjureType requestType
    )
    {
        var body = _componentCreature.ComponentBody;
        var controller = body.Character;
        foreach (var child in body.ChildBodies)
        {
            controller ??= child.Character;
        }

        var runtime = WorldRuntimeContext.For(Project);
        if (runtime.IsAuthority)
        {
            if (controller is null || controller.CharacterData.IsLocalCharacter)
            {
                Injure(amount, attacker, false, cause);
            }

            return;
        }

        if (controller is { CharacterData.IsLocalCharacter: true })
        {
            NetworkSender.SendToServer(
                runtime,
                EntityStatePackageBuilder.Capture(this, null, amount, cause, false, true, requestType)
            );
        }
    }

    public event Action<ComponentCreature>? Attacked;

    public void Heal(float amount)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (_componentCharacter is
            { ComponentSleep: { SubsystemUpdate.UpdatesPerFrame: 1, IsSleeping: true } })
        {
            amount *= _subsystemGameInfo.WorldSettings.RecoverFactor;
        }

        if (amount > 0f)
        {
            Health = MathUtils.Min(Health + amount, 1f);
        }
    }

    public void Injure(float amount, ComponentCreature? attacker, bool ignoreInvulnerability, LocalizedText cause)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        var context = new CreatureInjuringContext(this, amount, attacker, ignoreInvulnerability, cause);
        WorldRuleHooks.For(Project).Invoke(context);
        if (!WorldRuntimeContext.IsProjectActive(context.Project)) { return; }

        if (context.Cancel ||
            !(context.Amount > 0f) ||
            (!context.IgnoreInvulnerability && IsInvulnerable))
        {
            return;
        }

        amount = context.Amount;
        attacker = context.Attacker;
        ignoreInvulnerability = context.IgnoreInvulnerability;
        cause = context.Cause;

        ApplyInjury(amount, attacker, cause);
        NetworkSender.SendToObservers(
            Entity,
            EntityStatePackageBuilder.Capture(this, attacker, amount, cause, ignoreInvulnerability)
        );
        SendCombatStatistics(_componentCharacter);
        if (Health == 0f &&
            attacker?.Entity.FindComponent<ComponentCharacter>() is { } killer &&
            !ReferenceEquals(killer, _componentCharacter))
        {
            SendCombatStatistics(killer);
        }
    }

    private static void SendCombatStatistics(ComponentCharacter? character)
    {
        if (character is not null &&
            WorldRuntimeContext.IsProjectActive(character.Project) &&
            character.CharacterData.ControllerConnection is { IsConnected: true } owner)
        {
            NetworkSender.SendTo(
                owner,
                CharacterPackageBuilder.Capture(character, ComponentCharacterPackage.CharacterAction.SyncStat)
            );
        }
    }

    private void ApplyInjury(float amount, ComponentCreature? attacker, LocalizedText cause)
    {
        if (Health > 0f)
        {
            if (_componentCreature.CharacterStats != null)
            {
                if (attacker != null)
                {
                    _componentCreature.CharacterStats.HitsReceived++;
                }

                _componentCreature.CharacterStats.TotalHealthLost += MathUtils.Min(amount, Health);
            }

            Health = MathUtils.Max(Health - amount, 0f);
            if (Health == 0f)
            {
                DeathTime = _subsystemGameInfo.TotalElapsedGameTime;
                CauseOfDeath = cause;

                if (attacker != null)
                {
                    var player = Entity.FindComponent<ComponentCharacter>();
                    var player2 = attacker.Entity.FindComponent<ComponentCharacter>();
                    if (player != null && player2 != null)
                    {
                        CauseOfDeath =
                            LocalizedText.Format(
                                new LocalizedText(Name, "KilledByCharacter", "Killed by {0}"),
                                LocalizedText.Literal(player2.CharacterData.Name)
                            );
                    }
                }

                _componentCreature.CharacterStats?.AddDeathRecord(
                    new CharacterStats.DeathRecord
                    {
                        Day = _subsystemTimeOfDay.Day,
                        Location = _componentCreature.ComponentBody.Position,
                        Cause = cause
                    }
                );

                var componentCharacter = attacker?.Entity.FindComponent<ComponentCharacter>();
                if (componentCharacter is not null)
                {
                    if (_componentCharacter != null)
                    {
                        componentCharacter.CharacterStats.PlayerKills++;
                    }
                    else if (_componentCreature.Category is CreatureCategory.LandPredator or CreatureCategory.LandOther)
                    {
                        componentCharacter.CharacterStats.LandCreatureKills++;
                    }
                    else if (_componentCreature.Category is CreatureCategory.WaterPredator
                             or CreatureCategory.WaterOther)
                    {
                        componentCharacter.CharacterStats.WaterCreatureKills++;
                    }
                    else
                    {
                        componentCharacter.CharacterStats.AirCreatureKills++;
                    }

                    var num = (int)MathUtils.Ceiling(_componentCreature.ComponentHealth.AttackResilience / 12f);

                    for (var i = 0; i < num; i++)
                    {
                        var vector = _random.Vector2(2.5f, 3.5f);
                        _subsystemPickables.AddPickable(
                            248,
                            1,
                            _componentCreature.ComponentBody.Position,
                            new Vector3(vector.X, 6f, vector.Y),
                            null
                        );
                    }
                }
            }
        }

        if (attacker != null)
        {
            Attacked?.Invoke(attacker);
        }
    }

    public void ApplyHealthState(float health, LocalizedText cause, double? deathTime)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority) { return; }

        Health = health;
        CauseOfDeath = cause;
        DeathTime = deathTime;
    }

    public void ApplyInjuryState(float health, LocalizedText cause, double? deathTime, ComponentCreature? attacker)
    {
        ApplyHealthState(health, cause, deathTime);
        if (!WorldRuntimeContext.For(Project).IsAuthority && attacker is not null) { Attacked?.Invoke(attacker); }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentCharacter = Entity.FindComponent<ComponentCharacter>();
        _componentOnFire = Entity.FindComponent<ComponentOnFire>(true)!;
        AttackResilience = valuesDictionary.GetValue<float>("AttackResilience");
        FallResilience = valuesDictionary.GetValue<float>("FallResilience");
        FireResilience = valuesDictionary.GetValue<float>("FireResilience");
        CorpseDuration = valuesDictionary.GetValue<float>("CorpseDuration");
        BreathingMode = valuesDictionary.GetValue<BreathingMode>("BreathingMode");
        CanStrand = valuesDictionary.GetValue<bool>("CanStrand");
        Health = valuesDictionary.GetValue<float>("Health");
        Air = valuesDictionary.GetValue<float>("Air");
        AirCapacity = valuesDictionary.GetValue<float>("AirCapacity");
        var value = valuesDictionary.GetValue<double>("DeathTime");
        DeathTime = value >= 0.0 ? new double?(value) : null;
        CauseOfDeath = valuesDictionary.ContainsKey("CauseOfDeath")
            ? Localization.LocalizedTextStorage.Load(valuesDictionary.GetValue<ValuesDictionary>("CauseOfDeath"))
            : LocalizedText.Empty;
        if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative &&
            Entity.FindComponent<ComponentCharacter>() != null)
        {
            IsInvulnerable = true;
        }
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("Health", Health);
        valuesDictionary.SetValue("Air", Air);
        if (DeathTime.HasValue)
        {
            valuesDictionary.SetValue("DeathTime", DeathTime.Value);
        }

        if (CauseOfDeath != LocalizedText.Empty)
        {
            valuesDictionary.SetValue("CauseOfDeath", Localization.LocalizedTextStorage.Save(CauseOfDeath));
        }
    }
}
