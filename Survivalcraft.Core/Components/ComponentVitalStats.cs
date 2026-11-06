using System.Globalization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentVitalStats : Component, IUpdateable
{
    private const string _typeName = nameof(ComponentVitalStats);

    private ComponentCharacter _character = null!;

    private SubsystemGameInfo _info = null!;

    private SubsystemTime _time = null!;

    private SubsystemMetersBlockBehavior _meters = null!;

    private SubsystemWeather _weather = null!;

    private readonly Random _random = new();

    private readonly Dictionary<int, float> _satiation = new();

    private float _temperatureFlux;

    private float _densityModifier;

    private double? _lastAttackedTime;

    private Vector3? _lastPosition;

    private bool _wasAscending;

    private double _lastGroundedTime = double.NegativeInfinity;

    public float Food { get; set => field = MathUtils.Saturate(value); }

    public float Stamina { get; set => field = MathUtils.Saturate(value); }

    public float Sleep { get; set => field = MathUtils.Saturate(value); }

    public float Temperature { get; set => field = MathUtils.Clamp(value, 0, 24); }

    public float Wetness { get; set => field = MathUtils.Saturate(value); }

    public float EnvironmentTemperature { get; private set; }

    public float SleepBlackoutDuration { get; private set; }

    public long SleepBlackoutSequence { get; private set; }

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_character.ComponentHealth.Health <= 0)
        {
            ApplyDensityModifier(0);
            return;
        }

        var authority = WorldRuntimeContext.For(Project).IsAuthority;
        if (!authority && !_character.IsLocallyControlled) { return; }

        var body = _character.ComponentBody;
        var water = IsSwimming;
        var velocity = body.Velocity - body.StandingOnVelocity;
        var speed = velocity.XZ.Length();
        if (_lastPosition is { } last && dt > 0)
        {
            speed = MathUtils.Max(speed, ((body.Position - last).XZ / dt - body.StandingOnVelocity.XZ).Length());
        }

        _lastPosition = body.Position;
        if (body.StandingOnValue.HasValue || body.StandingOnBody is not null) { _lastGroundedTime = _time.GameTime; }

        var ascending = velocity.Y > 1;
        var jumped = ascending && !_wasAscending && _time.GameTime - _lastGroundedTime < .3;
        _wasAscending = ascending;
        var effort = body.ParentBody is null
            ? MathUtils.Saturate(
                speed /
                MathUtils.Max(1, _character.ComponentLocomotion.WalkSpeed * _character.ComponentLevel.SpeedFactor)
            )
            : 0f;
        var survival = _info.WorldSettings.GameMode >= GameMode.Survival &&
                       _info.WorldSettings.AreAdventureSurvivalMechanicsEnabled;
        Stamina = survival
            ? StaminaSimulation.Step(
                Stamina,
                dt,
                effort,
                jumped,
                water,
                _character.ComponentLevel.SpeedFactor,
                _character.ComponentSickness.IsSick || _character.ComponentFlu.HasFlu
            )
            : 1;
        ApplyDensityModifier(
            survival &&
            MathUtils.Saturate(3 * (.33f - Stamina)) >
            SimplexNoise.Noise((float)MathUtils.Remainder(_info.TotalElapsedGameTime, 1000))
                ? .6f
                : 0
        );
        SleepBlackoutDuration = MathUtils.Max(SleepBlackoutDuration - dt, 0);
        if (!authority) { return; }

        UpdateFood(dt, effort, jumped);
        UpdateSleep(dt);
        UpdateTemperature(dt);
        UpdateWetness(dt);
        if (survival && water && Stamina < .1f)
        {
            if (_time.PeriodicGameTimeEvent(5, 0))
            {
                _character.ComponentHealth.Injure(.05f, null, false, new LocalizedText(_typeName, "16", "16"));
            }

            if (_random.Bool(dt)) { body.ApplyImpulse(Vector3.UnitY * _character.ComponentLocomotion.JumpSpeed); }
        }

        if (Time.PeriodicEvent(1, .5) && _character.CharacterData.ControllerConnection is { IsConnected: true } owner)
        {
            NetworkSender.SendTo(owner, EntityStatePackageBuilder.Capture(this));
        }
    }

    public bool IsUnderwater => _character.ComponentBody.ImmersionDepth >
                                _character.ComponentCreaturePose.EyePosition.Y - _character.ComponentBody.Position.Y;

    public bool IsSwimming =>
        IsUnderwater || _character.ComponentBody is { ImmersionFactor: > .33f, StandingOnValue: null };

    public bool Eat(IInventory inventory, int slotIndex, int value)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority ||
            !ReferenceEquals(inventory.Project, Project) ||
            slotIndex < 0 ||
            slotIndex >= inventory.SlotsCount) { return false; }

        var content = Terrain.ExtractContents(value);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[content];
        var nutrition = block.GetNutritionalValue(value);
        var sickness = block.GetSicknessProbability(value);
        if (nutrition <= 0) { return false; }

        if (_character.ComponentSickness.IsSick && sickness > 0)
        {
            SendFeedback(1, false, true);
            return false;
        }

        if (Food >= .98f)
        {
            SendFeedback(2, false, true);
            return false;
        }

        if (_character.ComponentSickness.IsSick) { nutrition *= .75f; }

        Food += nutrition;
        _satiation.TryGetValue(content, out var eaten);
        eaten += MathUtils.Max(nutrition, .5f);
        _satiation[content] = eaten;
        var key = sickness >= .5f ? 3 : sickness > 0 ? 4 : eaten > 2.5f ? 5 : eaten > 2 ? 6 : Food > .85f ? 7 : 8;
        if (_character.ComponentSickness.IsSick) { _character.ComponentSickness.NauseaEffect(); }

        if (_random.Bool(sickness) || eaten > 3.5f) { _character.ComponentSickness.StartSickness(); }

        _character.CharacterStats.FoodItemsEaten++;
        SendFeedback(key, true, false);
        if (_character.CharacterData.ControllerConnection is { IsConnected: true } owner)
        {
            NetworkSender.SendTo(owner, EntityStatePackageBuilder.Capture(this));
            NetworkSender.SendTo(owner, CharacterPackageBuilder.CaptureSurvivalStatistics(_character));
        }

        return true;
    }

    private void SendFeedback(int key, bool eating, bool ownerOnly)
    {
        var message = new VitalFeedbackPackage { EntityId = Entity.EntityId, MessageKey = key, IsEating = eating };
        if (ownerOnly)
        {
            if (_character.CharacterData.ControllerConnection is { IsConnected: true } owner)
            {
                NetworkSender.SendTo(owner, message);
            }
        }
        else { NetworkSender.SendToObservers(Entity, message); }
    }

    public void MakeSleepy(float sleepValue)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority) { Sleep = MathUtils.Min(Sleep, sleepValue); }
    }

    private bool RulesEnabled => _info.WorldSettings.GameMode != GameMode.Creative &&
                                 _info.WorldSettings.AreAdventureSurvivalMechanicsEnabled;

    private void UpdateFood(float dt, float effort, bool jumped)
    {
        if (RulesEnabled)
        {
            var hunger = _character.ComponentLevel.HungerFactor;
            if (_character.ComponentSleep is { IsSleeping: true, SubsystemUpdate.UpdatesPerFrame: 1 })
            {
                hunger *= _info.WorldSettings.RecoverFactor;
            }

            Food -= hunger * dt / 2880;
            Food -= hunger * dt * effort / (IsSwimming ? 1440 : 2880);
            if (jumped) { Food -= hunger / 1200; }

            if (_character.ComponentMiner.DigCellFace.HasValue) { Food -= hunger * dt / 2880; }

            if (!_character.ComponentSleep.IsSleeping && Food <= 0 && _time.PeriodicGameTimeEvent(50, 0))
            {
                _character.ComponentHealth.Injure(.05f, null, false, new LocalizedText(_typeName, "9", "9"));
            }
        }
        else { Food = .9f; }

        if (_time.PeriodicGameTimeEvent(1, -.01))
        {
            foreach (var entry in _satiation.ToArray())
            {
                var remaining = MathUtils.Max(entry.Value - .000416666677f, 0);
                if (remaining > 0) { _satiation[entry.Key] = remaining; }
                else { _satiation.Remove(entry.Key); }
            }
        }
    }

    private void UpdateSleep(float dt)
    {
        if (!RulesEnabled)
        {
            Sleep = .9f;
            return;
        }

        if (_character.ComponentSleep.SleepFactor.CloseTo(1))
        {
            Sleep += .05f * dt;
            return;
        }

        if (_character.ComponentBody.ImmersionFactor > .05f ||
            _lastAttackedTime.HasValue && _time.GameTime - _lastAttackedTime <= 10) { return; }

        Sleep -= dt / 1800;
        if (Sleep < .075f)
        {
            var probability = MathUtils.Lerp(.05f, .2f, (.075f - Sleep) / .075f);
            if (_random.Bool(probability * dt))
            {
                SleepBlackoutDuration = MathUtils.Max(
                    SleepBlackoutDuration,
                    Sleep < .0375f ? _random.Float(3, 6) : _random.Float(2, 4)
                );
                SleepBlackoutSequence++;
            }
        }

        if (Sleep <= 0 && !_character.ComponentSleep.IsSleeping) { _character.ComponentSleep.Sleep(false); }
    }

    private void UpdateTemperature(float dt)
    {
        var clothing = _character.ComponentClothing;
        var insulation = clothing.Insulation * MathUtils.Lerp(1, .05f, MathUtils.Saturate(4 * Wetness));
        if (_info.WorldSettings.GameMode <= GameMode.Survival) { insulation = insulation * 1.5f + 1; }

        if (_time.PeriodicGameTimeEvent(2, 2.0 * GetHashCode() % 1000 / 1000))
        {
            var p = _character.ComponentBody.Position;
            _meters.CalculateTemperature(
                Terrain.ToCell(p.X),
                Terrain.ToCell(p.Y + .1f),
                Terrain.ToCell(p.Z),
                12,
                insulation,
                out var temperature,
                out _temperatureFlux
            );
            EnvironmentTemperature = temperature;
        }

        if (!RulesEnabled)
        {
            Temperature = 12;
            return;
        }

        var rate = .01f + .005f * _temperatureFlux;
        if (EnvironmentTemperature < Temperature)
        {
            rate *= clothing.Insulation >= 6 ? .1f : clothing.Insulation > 4 ? .25f : 1;
        }

        Temperature += MathUtils.Saturate(rate * dt) * (EnvironmentTemperature - Temperature);
        if (Temperature <= 0)
        {
            _character.ComponentHealth.Injure(1, null, false, new LocalizedText(_typeName, "25", "25"));
        }
        else if (Temperature < 3 && _time.PeriodicGameTimeEvent(10, 0))
        {
            _character.ComponentHealth.Injure(.05f, null, false, new LocalizedText(_typeName, "26", "26"));
        }

        if (Temperature >= 24 && _time.PeriodicGameTimeEvent(10, 0))
        {
            _character.ComponentHealth.Injure(.05f, null, false, new LocalizedText(_typeName, "35", "35"));
        }
    }

    private void UpdateWetness(float dt)
    {
        var body = _character.ComponentBody;
        if (body is { ImmersionFactor: > .2f, ImmersionFluidBlock: WaterBlock })
        {
            Wetness += MathUtils.Saturate(3 * dt) * (2 * body.ImmersionFactor - Wetness);
        }

        var p = body.Position;
        var rain = _weather.GetPrecipitationShaftInfo(Terrain.ToCell(p.X), Terrain.ToCell(p.Z));
        if (Terrain.ToCell(p.Y + .1f) >= rain.YLimit && rain.Type == PrecipitationType.Rain)
        {
            Wetness += .05f * rain.Intensity * dt;
        }

        Wetness -= dt /
                   (EnvironmentTemperature > 24 ? 30 :
                       EnvironmentTemperature > 16 ? 60 :
                       EnvironmentTemperature > 8 ? 120 : 180);
    }

    private void ApplyDensityModifier(float modifier)
    {
        _character.ComponentBody.Density += modifier - _densityModifier;
        _densityModifier = modifier;
    }

    public void ApplyState(VitalStatePackage state)
    {
        if (WorldRuntimeContext.For(Project).IsAuthority) { return; }

        Food = state.Food;
        Stamina = state.Stamina;
        Sleep = state.Sleep;
        Temperature = state.Temperature;
        Wetness = state.Wetness;
        EnvironmentTemperature = state.EnvironmentTemperature;
        if (state.SleepBlackoutSequence > SleepBlackoutSequence)
        {
            SleepBlackoutSequence = state.SleepBlackoutSequence;
            SleepBlackoutDuration = state.SleepBlackoutDuration;
        }
    }

    private void OnAttacked(ComponentCreature attacker) => _lastAttackedTime = _time.GameTime;

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _info = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _time = Project.FindSubsystem<SubsystemTime>(true)!;
        _meters = Project.FindSubsystem<SubsystemMetersBlockBehavior>(true)!;
        _weather = Project.FindSubsystem<SubsystemWeather>(true)!;
        _character = Entity.FindComponent<ComponentCharacter>(true)!;
        Food = valuesDictionary.GetValue<float>("Food");
        Stamina = valuesDictionary.GetValue<float>("Stamina");
        Sleep = valuesDictionary.GetValue<float>("Sleep");
        Temperature = valuesDictionary.GetValue<float>("Temperature");
        Wetness = valuesDictionary.GetValue<float>("Wetness");
        EnvironmentTemperature = valuesDictionary.GetValue("EnvironmentTemperature", Temperature);
        SleepBlackoutDuration = valuesDictionary.GetValue("SleepBlackoutDuration", 0f);
        SleepBlackoutSequence = valuesDictionary.GetValue("SleepBlackoutSequence", 0L);
        foreach (var item in valuesDictionary.GetValue<ValuesDictionary>("Satiation"))
        {
            _satiation[int.Parse(item.Key, CultureInfo.InvariantCulture)] = (float)item.Value;
        }

        _character.ComponentHealth.Attacked += OnAttacked;
    }

    public override void Dispose()
    {
        if (_character is not null) { _character.ComponentHealth.Attacked -= OnAttacked; }
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("Food", Food);
        valuesDictionary.SetValue("Stamina", Stamina);
        valuesDictionary.SetValue("Sleep", Sleep);
        valuesDictionary.SetValue("Temperature", Temperature);
        valuesDictionary.SetValue("Wetness", Wetness);
        valuesDictionary.SetValue("EnvironmentTemperature", EnvironmentTemperature);
        valuesDictionary.SetValue("SleepBlackoutDuration", SleepBlackoutDuration);
        valuesDictionary.SetValue("SleepBlackoutSequence", SleepBlackoutSequence);
        var satiation = new ValuesDictionary();
        foreach (var item in _satiation)
        {
            satiation.SetValue(item.Key.ToString(CultureInfo.InvariantCulture), item.Value);
        }

        valuesDictionary.SetValue("Satiation", satiation);
    }
}
