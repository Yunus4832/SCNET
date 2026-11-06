using Engine.Audio;
using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public sealed class ComponentVitalStatsPresentation : Component, IUpdateable
{
    private const string _typeName = nameof(ComponentVitalStats);

    private ComponentCharacter _character = null!;

    private ComponentVitalStats _vitals = null!;

    private SubsystemTime _time = null!;

    private SubsystemGameInfo _info = null!;

    private SubsystemAudio _audio = null!;

    private Sound? _panting;

    private readonly Random _random = new();

    private float _lastFood;

    private float _lastStamina;

    private float _lastSleep;

    private float _lastTemperature;

    private float _lastWetness;

    private float _sleepBlackout;

    private float _heatBlackout;

    private float _heatDuration;

    private long _lastBlackoutSequence;

    private bool _wasSleeping;

    private bool _inactive;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.CreatureModels + 1;

    public void Update(float dt)
    {
        if (_inactive || !IsAddedToProject || !WorldRuntimeContext.For(Project).HasPresentation) { return; }

        if (_character.ComponentHealth.Health <= 0)
        {
            StopSound();
            return;
        }

        if (!_character.IsLocallyControlled) { return; }

        UpdatePanting();
        var survival = _info.WorldSettings.GameMode != GameMode.Creative &&
                       _info.WorldSettings.AreAdventureSurvivalMechanicsEnabled;
        if (survival)
        {
            UpdateFoodWarnings();
            UpdateStaminaWarnings();
            UpdateSleepWarnings(dt);
            UpdateTemperatureWarnings(dt);
            UpdateWetnessWarnings();
        }
        else
        {
            _sleepBlackout = _heatBlackout = _heatDuration = 0;
        }

        _character.Entity.FindComponent<ComponentGui>(true)!.FoodBarWidget.Value = _vitals.Food;
        var index = _vitals.EnvironmentTemperature switch
        {
            > 22 => 6, > 18 => 5, > 14 => 4, > 10 => 3, > 6 => 2, > 2 => 1, _ => 0
        };
        _character.Entity.FindComponent<ComponentGui>(true)!.TemperatureBarWidget.BarSubtexture =
            ContentManager.Get<Subtexture>("Textures/Atlas/Temperature" + index);
        _lastFood = _vitals.Food;
        _lastStamina = _vitals.Stamina;
        _lastSleep = _vitals.Sleep;
        _lastTemperature = _vitals.Temperature;
        _lastWetness = _vitals.Wetness;
        _wasSleeping = _character.ComponentSleep.IsSleeping;
    }

    private void Notify(int key, bool important = true, params object[] arguments)
    {
        var text = Localization.ClientLanguage.Catalog.Get(_typeName, key);
        if (arguments.Length > 0) { text = string.Format(text, arguments); }

        _character.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(text, Color.White, true, important);
    }

    private void UpdateFoodWarnings()
    {
        if (_character.ComponentSleep.IsSleeping) { return; }

        var repeat = _time.PeriodicGameTimeEvent(240, 13) && !_character.ComponentSickness.IsSick;
        if (_vitals.Food <= 0 && _time.PeriodicGameTimeEvent(50, 0))
        {
            Notify(10, false);
            _character.Entity.FindComponent<ComponentGui>(true)!.FoodBarWidget.Flash(10);
        }
        else if (_vitals.Food < .1f && (_lastFood >= .1f || repeat)) { Notify(11); }
        else if (_vitals.Food < .25f && (_lastFood >= .25f || repeat)) { Notify(12); }
        else if (_vitals.Food < .5f && (_lastFood >= .5f || repeat)) { Notify(13, false); }
    }

    private void UpdateStaminaWarnings()
    {
        if (!_vitals.IsSwimming && _vitals.Stamina < .33f && _lastStamina >= .33f) { Notify(14, false); }

        if (_vitals.IsSwimming && _vitals.Stamina < .4f && _lastStamina >= .4f) { Notify(15); }

        if (_vitals.Stamina < .1f && _time.PeriodicGameTimeEvent(5, 0))
        {
            Notify(_vitals.IsSwimming ? 17 : 18, !_vitals.IsSwimming);
        }
    }

    private void UpdateSleepWarnings(float dt)
    {
        var sleep = _character.ComponentSleep;
        if (sleep.IsSleeping && !_wasSleeping && !sleep.AllowManualWakeUp)
        {
            Notify(23);
            _character.Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!.PlayMoanSound();
        }

        if (!sleep.IsSleeping)
        {
            var repeat = _time.PeriodicGameTimeEvent(240, 9);
            var key = _vitals.Sleep < .075f && (_lastSleep >= .075f || repeat) ? 19 :
                _vitals.Sleep < .2f && (_lastSleep >= .2f || repeat) ? 20 :
                _vitals.Sleep < .33f && (_lastSleep >= .33f || repeat) ? 21 :
                _vitals.Sleep < .5f && (_lastSleep >= .5f || repeat) ? 22 : 0;
            if (key != 0)
            {
                Notify(key, key <= 20);
                if (key <= 20)
                {
                    _character.Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!.PlayMoanSound();
                }
            }
        }

        if (_vitals.SleepBlackoutSequence > _lastBlackoutSequence)
        {
            _lastBlackoutSequence = _vitals.SleepBlackoutSequence;
            _character.Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!.PlayMoanSound();
        }

        var target = MathUtils.Saturate(.5f * _vitals.SleepBlackoutDuration);
        _sleepBlackout = MathUtils.Saturate(_sleepBlackout + 2 * dt * (target - _sleepBlackout));
        if (!sleep.IsSleeping)
        {
            var overlays = _character.Entity.FindComponent<ComponentScreenOverlays>(true)!;
            overlays.BlackoutFactor = MathUtils.Max(overlays.BlackoutFactor, _sleepBlackout);
            if (_sleepBlackout > .01f)
            {
                overlays.FloatingMessage = Localization.ClientLanguage.Catalog.Get(_typeName, 24);
                overlays.FloatingMessageFactor = MathUtils.Saturate(10 * (_sleepBlackout - .9f));
            }
        }
    }

    private void UpdateTemperatureWarnings(float dt)
    {
        var clothing = _character.ComponentClothing;
        var slotKey = clothing.LeastInsulatedSlot switch
        {
            ClothingSlot.Head => 41, ClothingSlot.Torso => 42, ClothingSlot.Legs => 43, _ => 44
        };
        var bodyPart = Localization.ClientLanguage.Catalog.Get(_typeName, slotKey);
        var insulation = clothing.Insulation * MathUtils.Lerp(1, .05f, MathUtils.Saturate(4 * _vitals.Wetness));
        if (_info.WorldSettings.GameMode <= GameMode.Survival) { insulation = insulation * 1.5f + 1; }

        var repeat = _time.PeriodicGameTimeEvent(300, 17);
        if (_vitals.Temperature < 3 && _time.PeriodicGameTimeEvent(10, 0))
        {
            Notify(_vitals.Wetness > 0 ? 27 : insulation >= 1 ? 28 : 29, false, bodyPart);
            _character.Entity.FindComponent<ComponentGui>(true)!.TemperatureBarWidget.Flash(10);
        }
        else if (_vitals.Temperature < 6 && (_lastTemperature >= 6 || repeat))
        {
            Notify(_vitals.Wetness > 0 ? 30 : insulation >= 1 ? 31 : 32, true, bodyPart);
            _character.Entity.FindComponent<ComponentGui>(true)!.TemperatureBarWidget.Flash(10);
        }
        else if (_vitals.Temperature < 8 && (_lastTemperature >= 8 || repeat))
        {
            Notify(33, false);
            _character.Entity.FindComponent<ComponentGui>(true)!.TemperatureBarWidget.Flash(10);
        }

        if (_vitals.Temperature >= 24)
        {
            if (_time.PeriodicGameTimeEvent(10, 0))
            {
                Notify(34, false);
                _character.Entity.FindComponent<ComponentGui>(true)!.TemperatureBarWidget.Flash(10);
            }

            if (_time.PeriodicGameTimeEvent(8, 0))
            {
                _heatDuration = MathUtils.Max(_heatDuration, 6);
                _character.Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!.PlayMoanSound();
            }
        }
        else if (_vitals.Temperature > 20 && _time.PeriodicGameTimeEvent(10, 0))
        {
            Notify(36, false);
            _heatDuration = MathUtils.Max(_heatDuration, 3);
            _character.Entity.FindComponent<ComponentGui>(true)!.TemperatureBarWidget.Flash(10);
            _character.Entity.FindComponent<ComponentCreatureSoundsPresentation>(true)!.PlayMoanSound();
        }

        var overlays = _character.Entity.FindComponent<ComponentScreenOverlays>(true)!;
        overlays.IceFactor = MathUtils.Saturate(1 - _vitals.Temperature / 6);
        _heatDuration = MathUtils.Max(_heatDuration - dt, 0);
        _heatBlackout = MathUtils.Saturate(
            _heatBlackout + 2 * dt * (MathUtils.Saturate(.5f * _heatDuration) - _heatBlackout)
        );
        overlays.BlackoutFactor = MathUtils.Max(overlays.BlackoutFactor, _heatBlackout);
        if (_heatBlackout > .01f)
        {
            overlays.FloatingMessage = Localization.ClientLanguage.Catalog.Get(_typeName, 37);
            overlays.FloatingMessageFactor = MathUtils.Saturate(10 * (_heatBlackout - .9f));
        }
    }

    private void UpdateWetnessWarnings()
    {
        var key = _vitals.Wetness > .8f && _lastWetness <= .8f ? 38 :
            _vitals.Wetness > .2f && _lastWetness <= .2f ? 39 :
            _vitals.Wetness <= 0 && _lastWetness > 0 ? 40 : 0;
        if (key == 0) { return; }

        WorldRuntimeContext.For(Project).QueueTimeDelayedExecution(
            Time.FrameStartTime + 2,
            () =>
            {
                if (!_inactive &&
                    IsAddedToProject &&
                    WorldRuntimeContext.IsProjectActive(Project) &&
                    (key == 38 && _vitals.Wetness > .8f ||
                     key == 39 && _vitals.Wetness is > .2f and <= .8f ||
                     key == 40 && _vitals.Wetness <= 0))
                {
                    Notify(key);
                }
            }
        );
    }

    private void UpdatePanting()
    {
        var volume = MathUtils.Saturate(2 * (.5f - _vitals.Stamina));
        if (_info.WorldSettings.GameMode < GameMode.Survival ||
            !_info.WorldSettings.AreAdventureSurvivalMechanicsEnabled ||
            _vitals.IsUnderwater ||
            volume <= 0)
        {
            StopSound();
            return;
        }

        if (_panting is null)
        {
            _panting = _audio.CreateSound("Audio/HumanPanting");
            _panting.IsLooped = true;
        }

        _panting.Volume = Configuration.ClientConfiguration.Preferences.SoundsVolume *
                          volume *
                          MathUtils.Lerp(
                              .8f,
                              1,
                              SimplexNoise.Noise((float)MathUtils.Remainder(3 * Time.RealTime + 100, 1000))
                          );
        var sexPitch = _character.CharacterData.CharacterSex == CharacterSex.Female ? .2f : 0;
        _panting.Pitch = AudioManager.ToEnginePitch(
            sexPitch +
            MathUtils.Lerp(-.15f, .05f, volume) *
            MathUtils.Lerp(.8f, 1.2f, SimplexNoise.Noise((float)MathUtils.Remainder(3 * Time.RealTime + 200, 1000)))
        );
        _panting.Play();
    }

    internal void ApplyFeedback(int key, bool eating)
    {
        if (_inactive || !WorldRuntimeContext.For(Project).HasPresentation) { return; }

        if (eating)
        {
            _audio.PlayRandomSound(
                "Audio/Creatures/HumanEat",
                1,
                _random.Float(-.2f, .2f),
                _character.ComponentBody.Position,
                2,
                0
            );
        }

        if (_character.IsLocallyControlled && key > 0) { Notify(key, key != 8); }
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _character = Entity.FindComponent<ComponentCharacter>(true)!;
        _vitals = Entity.FindComponent<ComponentVitalStats>(true)!;
        _time = Project.FindSubsystem<SubsystemTime>(true)!;
        _info = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _lastFood = _vitals.Food;
        _lastSleep = _vitals.Sleep;
        _lastStamina = _vitals.Stamina;
        _lastTemperature = _vitals.Temperature;
        _lastWetness = _vitals.Wetness;
        _lastBlackoutSequence = _vitals.SleepBlackoutSequence;
        _wasSleeping = _character.ComponentSleep.IsSleeping;
    }

    public override void OnEntityAdded() => _inactive = false;

    public override void OnEntityRemoved()
    {
        _inactive = true;
        StopSound();
    }

    public override void Dispose()
    {
        _inactive = true;
        StopSound();
    }

    private void StopSound()
    {
        _panting?.Dispose();
        _panting = null;
    }
}
