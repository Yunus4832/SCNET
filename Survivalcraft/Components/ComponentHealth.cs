using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Components;

public class ComponentHealth : Component, IUpdateable
{
    public const string Name = "ComponentHealth";

    private ComponentCreature _componentCreature = null!;

    private ComponentOnFire _componentOnFire = null!;

    private ComponentPlayer? _componentPlayer;

    private float _lastHealth;

    private readonly Random _random = new();

    public float RedScreenFactor;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private SubsystemTimeOfDay _subsystemTimeOfDay = null!;

    private bool _wasStanding;

    public string CauseOfDeath { get; set; } = string.Empty;

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
            if (CommonLib.WorkType != WorkType.Client && Time.PeriodicEvent(2.0, 0.0))
            {
                CommonLib.Net.QueuePackage(new ComponentHealthPackage(this));
            }

            var num = 0f;
            if (_componentPlayer != null)
            {
                if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Harmless)
                {
                    num = 0.0166666675f;
                }
                else if (_componentPlayer.ComponentSleep.SleepFactor.CloseTo(1f) &&
                         _componentPlayer.ComponentVitalStats.Food > 0f)
                {
                    num = 0.00166666671f;
                }
                else if (_componentPlayer.ComponentVitalStats.Food > 0.5f)
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
                Terrain.ToCell(_componentCreature.ComponentCreatureModel.EyePosition.Y),
                Terrain.ToCell(position.Z)
            );
            Air = BlocksManager.Blocks[cellContents] is FluidBlock || position.Y > 700f
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
            var cause = LanguageManager.Get(Name, 1);
            Injure(2f * _componentCreature.ComponentBody.ImmersionFactor * dt, null, false, cause);
            var num2 = 1.1f + 0.1f * (float)MathUtils.Sin(12.0 * _subsystemTime.GameTime);
            RedScreenFactor = MathUtils.Max(RedScreenFactor,
                num2 * 1.5f * _componentCreature.ComponentBody.ImmersionFactor);
        }

        var num3 = MathUtils.Abs(_componentCreature.ComponentBody.CollisionVelocityChange.Y);
        if (!_wasStanding && num3 > FallResilience)
        {
            //掉落伤害，这里需要客户端计算伤害同步到服务器
            var num4 = MathUtils.Sqr(MathUtils.Max(num3 - FallResilience, 0f)) / 15f;
            if (_componentPlayer != null)
            {
                num4 /= _componentPlayer.ComponentLevel.ResilienceFactor;
            }

            var cause = LanguageManager.Get(Name, 2);
            var flagMainRider = _componentCreature.ComponentBody.ChildBodies.Count > 0 &&
                                _componentCreature.ComponentBody.ChildBodies[0].Player is
                                { PlayerData.IsMainPlayer: true };
            var flagMainPlayer = _componentPlayer is { PlayerData.IsMainPlayer: true };
            var flagNoRider = _componentCreature.ComponentBody.ChildBodies.Count == 0;
            if (CommonLib.WorkType == WorkType.Local)
            {
                Injure(num4, null, false, cause);
            }

            if (CommonLib.WorkType == WorkType.Server)
            {
                //是(主玩家、主玩家骑乘的生物、没有被玩家骑乘的生物)计算伤害
                if (flagMainPlayer || flagMainRider || flagNoRider)
                {
                    Injure(num4, null, false, cause);
                }
            }
            else
            {
                //是(主玩家、被主玩家骑乘的生物)计算伤害，请求给服务器
                if (flagMainPlayer || flagMainRider)
                {
                    CommonLib.Net.QueuePackage(new ComponentHealthPackage(this, null, num4, cause, false, true,
                        ComponentHealthPackage.RequestInjureType.Fall));
                }
            }
        }

        _wasStanding = _componentCreature.ComponentBody.StandingOnValue.HasValue ||
                       _componentCreature.ComponentBody.StandingOnBody != null;
        if (position.Y < 0f && _subsystemTime.PeriodicGameTimeEvent(2.0, 0.0))
        {
            //掉出世界伤害
            Injure(0.1f, null, true, LanguageManager.Get(Name, 3));
            _componentPlayer?.ComponentGui.DisplaySmallMessage(LanguageManager.Get(Name, 4), Color.White, true,
                false);
        }

        var num5 = _subsystemTime.PeriodicGameTimeEvent(1.0, 0.0);
        if (num5 && Air == 0f)
        {
            var num6 = 0.12f;
            if (_componentPlayer != null)
            {
                num6 /= _componentPlayer.ComponentLevel.ResilienceFactor;
            }

            //窒息伤害
            Injure(num6, null, false, LanguageManager.Get(Name, 7));
        }

        if (num5 && (_componentOnFire.IsOnFire || _componentOnFire.TouchesFire))
        {
            var flagMainRider = _componentCreature.ComponentBody.ChildBodies.Count > 0 &&
                                _componentCreature.ComponentBody.ChildBodies[0].Player is
                                { PlayerData.IsMainPlayer: true };
            var flagMainPlayer = _componentPlayer is { PlayerData.IsMainPlayer: true };
            var flagNoRider = _componentCreature.ComponentBody.ChildBodies.Count == 0;
            var num7 = 1f / FireResilience;
            if (_componentPlayer != null)
            {
                num7 /= _componentPlayer.ComponentLevel.ResilienceFactor;
            }

            var cause = LanguageManager.Get(Name, 5);
            //玩家 TouchesFire 伤害由客户端计算
            if (CommonLib.WorkType == WorkType.Client)
            {
                if (flagMainPlayer || flagMainRider)
                {
                    CommonLib.Net.QueuePackage(new ComponentHealthPackage(this, null, num7, cause, false, true,
                        ComponentHealthPackage.RequestInjureType.Fire));
                }
            }

            if (CommonLib.WorkType == WorkType.Server)
            {
                if (flagMainPlayer || flagMainRider || flagNoRider)
                {
                    Injure(num7, _componentOnFire.Attacker, false, LanguageManager.Get(Name, 5));
                }
            }
            else
            {
                Injure(num7, _componentOnFire.Attacker, false, LanguageManager.Get(Name, 5));
            }
        }

        if (num5 && CanStrand && _componentCreature.ComponentBody.ImmersionFactor < 0.25f &&
            (_componentCreature.ComponentBody.StandingOnValue != 0 ||
             _componentCreature.ComponentBody.StandingOnBody != null))
        //搁浅伤害
        {
            Injure(0.05f, null, false, LanguageManager.Get(Name, 6));
        }

        HealthChange = Health - _lastHealth;
        _lastHealth = Health;
        if (RedScreenFactor > 0.01f)
        {
            RedScreenFactor *= MathUtils.Pow(0.2f, dt);
        }
        else
        {
            RedScreenFactor = 0f;
        }

        if (HealthChange < 0f)
        {
            _componentCreature.ComponentCreatureSounds.PlayPainSound();
            RedScreenFactor += -4f * HealthChange;
            if (RunMode.Value is RunModeType.Gui)
            {
                _componentPlayer?.ComponentGui.HealthBarWidget.Flash(
                    MathUtils.Clamp((int)((0f - HealthChange) * 30f), 0, 10)
                );
            }
        }

        _componentPlayer?.ComponentScreenOverlays.RedOutFactor = MathUtils
            .Max(
                _componentPlayer.ComponentScreenOverlays.RedOutFactor,
                RedScreenFactor
            );

        if (RunMode.Value is RunModeType.Gui)
        {
            _componentPlayer?.ComponentGui.HealthBarWidget.Value = Health;
        }

        if (Health == 0f && HealthChange < 0f)
        {
            var position2 = _componentCreature.ComponentBody.Position +
                            new Vector3(0f, _componentCreature.ComponentBody.BoxSize.Y / 2f, 0f);
            var x = _componentCreature.ComponentBody.StanceBoxSize.X;
            if (RunMode.Value is RunModeType.Gui)
            {
                _subsystemParticles.AddParticleSystem(new KillParticleSystem(_subsystemTerrain, position2, x));
            }

            var position3 = (_componentCreature.ComponentBody.BoundingBox.Min +
                             _componentCreature.ComponentBody.BoundingBox.Max) / 2f;
            foreach (var item in Entity.FindComponents<IInventory>())
            {
                item?.DropAllItems(position3);
            }

            DeathTime = _subsystemGameInfo.TotalElapsedGameTime;
        }

        if (Health <= 0f && CorpseDuration > 0f &&
            _subsystemGameInfo.TotalElapsedGameTime - DeathTime > CorpseDuration)
        {
            _componentCreature.ComponentSpawn.Despawn();
        }
    }

    public event Action<ComponentCreature>? Attacked;

    public void Heal(float amount)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        //开服后，睡觉恢复速度加快5倍
        if (CommonLib.WorkType == WorkType.Server && _componentPlayer is
            { ComponentSleep: { SubsystemUpdate.UpdatesPerFrame: 1, IsSleeping: true } })
        {
            amount *= _subsystemGameInfo.WorldSettings.RecoverFactor;
        }

        if (amount > 0f)
        {
            Health = MathUtils.Min(Health + amount, 1f);
        }
    }

    public void Injure(float amount, ComponentCreature? attacker, bool ignoreInvulnerability, string cause)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        var context = new CreatureInjuringContext(this, amount, attacker, ignoreInvulnerability, cause);
        CurrentModRuntime.Value?.Gameplay.Invoke(context);
        if (context.Cancel || !(context.Amount > 0f) ||
            (!context.IgnoreInvulnerability && IsInvulnerable))
        {
            return;
        }

        amount = context.Amount;
        attacker = context.Attacker;
        ignoreInvulnerability = context.IgnoreInvulnerability;
        cause = context.Cause;

        NetInjure(amount, attacker, cause);
        CommonLib.Net.QueuePackage(new ComponentHealthPackage(this, attacker, amount, cause, ignoreInvulnerability));
    }

    public void NetInjure(float amount, ComponentCreature? attacker, string cause)
    {
        if (Health > 0f)
        {
            if (_componentCreature.PlayerStats != null)
            {
                if (attacker != null)
                {
                    _componentCreature.PlayerStats.HitsReceived++;
                }

                _componentCreature.PlayerStats.TotalHealthLost += MathUtils.Min(amount, Health);
            }

            Health = MathUtils.Max(Health - amount, 0f);
            if (Health == 0f)
            {
                CauseOfDeath = cause;
                if (CommonLib.WorkType != WorkType.Client)
                {
                    var componentHealthPackage = new ComponentHealthPackage(this)
                    {
                        Cause = CauseOfDeath
                    };
                    CommonLib.Net.QueuePackage(componentHealthPackage);
                }

                if (attacker != null)
                {
                    var player = Entity.FindComponent<ComponentPlayer>();
                    var player2 = attacker.Entity.FindComponent<ComponentPlayer>();
                    if (player != null && player2 != null)
                    {
                        CauseOfDeath =
                            $" 被 {player2.PlayerData.Name} 噶了";
                    }
                }

                _componentCreature.PlayerStats?.AddDeathRecord(new PlayerStats.DeathRecord
                {
                    Day = _subsystemTimeOfDay.Day,
                    Location = _componentCreature.ComponentBody.Position,
                    Cause = cause
                });

                var componentPlayer = attacker?.Entity.FindComponent<ComponentPlayer>();
                if (componentPlayer is not null)
                {
                    if (_componentPlayer != null)
                    {
                        componentPlayer.PlayerStats.PlayerKills++;
                    }
                    else if (_componentCreature.Category is CreatureCategory.LandPredator or CreatureCategory.LandOther)
                    {
                        componentPlayer.PlayerStats.LandCreatureKills++;
                    }
                    else if (_componentCreature.Category is CreatureCategory.WaterPredator
                             or CreatureCategory.WaterOther)
                    {
                        componentPlayer.PlayerStats.WaterCreatureKills++;
                    }
                    else
                    {
                        componentPlayer.PlayerStats.AirCreatureKills++;
                    }

                    var num = (int)MathUtils.Ceiling(_componentCreature.ComponentHealth.AttackResilience / 12f);

                    for (var i = 0; i < num; i++)
                    {
                        var vector = _random.Vector2(2.5f, 3.5f);
                        _subsystemPickables.AddPickable(248, 1, _componentCreature.ComponentBody.Position,
                            new Vector3(vector.X, 6f, vector.Y), null);
                    }
                }
            }
        }

        if (attacker != null)
        {
            Attacked?.Invoke(attacker);
        }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _componentCreature = Entity.FindComponent<ComponentCreature>(true)!;
        _componentPlayer = Entity.FindComponent<ComponentPlayer>();
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
        CauseOfDeath = valuesDictionary.GetValue<string>("CauseOfDeath");
        if (_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative &&
            Entity.FindComponent<ComponentPlayer>() != null)
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

        if (!string.IsNullOrEmpty(CauseOfDeath))
        {
            valuesDictionary.SetValue("CauseOfDeath", CauseOfDeath);
        }
    }
}
