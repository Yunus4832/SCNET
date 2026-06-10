using System.Globalization;

using Engine.Audio;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Components;

public class ComponentVitalStats : Component, IUpdateable
{
    private const string _typeName = "ComponentVitalStats";

    private readonly SafeFloat _food = new();

    private readonly SafeFloat _sleep = new();

    private readonly SafeFloat _stamina = new();

    private readonly SafeFloat _temperature = new();

    private readonly SafeFloat _wetness = new();

    private ComponentPlayer _componentPlayer = null!;

    private float _densityModifierApplied;

    private float _environmentTemperature;

    private float _environmentTemperatureFlux;

    private double? _lastAttackedTime;

    private float _lastFood;

    private float _lastSleep;

    private float _lastStamina;

    private float _lastTemperature;

    private float _lastWetness;

    private Sound? _pantingSound;

    private readonly Random _random = new();

    private readonly Dictionary<int, float> _satiation = new();

    private readonly List<KeyValuePair<int, float>> _satiationList = [];

    private float _sleepBlackoutDuration;

    private float _sleepBlackoutFactor;

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemMetersBlockBehavior _subsystemMetersBlockBehavior = null!;

    private SubsystemTime _subsystemTime = null!;

    private SubsystemWeather _subsystemWeather = null!;

    private float _temperatureBlackoutDuration;

    private float _temperatureBlackoutFactor;

    public float Food
    {
        get => _food.Get();
        set => _food.Set(MathUtils.Saturate(value));
    }

    public float Stamina
    {
        get => _stamina.Get();
        set => _stamina.Set(MathUtils.Saturate(value));
    }

    public float Sleep
    {
        get => _sleep.Get();
        set => _sleep.Set(MathUtils.Saturate(value));
    }

    public float Temperature
    {
        get => _temperature.Get();
        set => _temperature.Set(MathUtils.Clamp(value, 0f, 24f));
    }

    public float Wetness
    {
        get => _wetness.Get();
        set => _wetness.Set(MathUtils.Saturate(value));
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_componentPlayer.ComponentHealth.Health > 0f)
        {
            var runGui = RunMode.Value is RunModeType.Gui;
            UpdateFood(runGui);
            if (runGui)
            {
                UpdateStamina();
            }

            UpdateSleep(runGui);
            UpdateTemperature(runGui);
            UpdateWetness(runGui);
            if (_componentPlayer.ComponentSleep.SubsystemUpdate.IsLastUpdateInFrame &&
                Time.PeriodicEvent(1.0, 0.5))
            {
                CommonLib.Net.QueuePackage(new ComponentVitalStatPackage(this));
            }
        }
        else
        {
            DisposeSound();
        }
    }

    public bool Eat(IInventory inventory, int slotIndex, int value)
    {
        return NetEat(inventory, slotIndex, value);
    }

    public bool NetEat(IInventory inventory, int slotIndex, int value)
    {
        var num = Terrain.ExtractContents(value);
        var obj = BlocksManager.Blocks[num];
        var num2 = obj.GetNutritionalValue(value);
        var sicknessProbability = obj.GetSicknessProbability(value);
        if (!(num2 > 0f))
        {
            return false;
        }

        if (_componentPlayer.ComponentSickness.IsSick && sicknessProbability > 0f)
        {
            _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 1), Color.White, true,
                true);
            return false;
        }

        if (Food >= 0.98f)
        {
            _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 2), Color.White, true,
                true);
            return false;
        }

        _subsystemAudio.PlayRandomSound("Audio/Creatures/HumanEat", 1f, _random.Float(-0.2f, 0.2f),
            _componentPlayer.ComponentBody.Position, 2f, 0f);
        if (_componentPlayer.ComponentSickness.IsSick)
        {
            num2 *= 0.75f;
        }

        Food += num2;
        _satiation.TryGetValue(num, out var value2);
        value2 += MathUtils.Max(num2, 0.5f);
        _satiation[num] = value2;
        if (_componentPlayer.ComponentSickness.IsSick)
        {
            _componentPlayer.ComponentSickness.NauseaEffect();
        }
        else
        {
            switch (sicknessProbability)
            {
                case >= 0.5f:
                    _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 3), Color.White,
                        true,
                        true);
                    break;
                case > 0f:
                    _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 4), Color.White,
                        true,
                        true);
                    break;
                default:
                {
                    switch (value2)
                    {
                        case > 2.5f:
                            _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 5),
                                Color.White, true,
                                true);
                            break;
                        case > 2f:
                            _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 6),
                                Color.White, true,
                                true);
                            break;
                        default:
                        {
                            if (Food > 0.85f)
                            {
                                _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 7),
                                    Color.White, true,
                                    true);
                            }
                            else
                            {
                                _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 8),
                                    Color.White, true,
                                    false);
                            }

                            break;
                        }
                    }

                    break;
                }
            }
        }

        if (_random.Bool(sicknessProbability) || value2 > 3.5f)
        {
            _componentPlayer.ComponentSickness.StartSickness();
        }

        _componentPlayer.PlayerStats.FoodItemsEaten++;
        return true;
    }

    public void MakeSleepy(float sleepValue)
    {
        Sleep = MathUtils.Min(Sleep, sleepValue);
    }

    public void DisposeSound()
    {
        if (_pantingSound == null)
        {
            return;
        }

        _pantingSound.Stop();
        _pantingSound.Dispose();
        _pantingSound = null;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemMetersBlockBehavior = Project.FindSubsystem<SubsystemMetersBlockBehavior>(true)!;
        _subsystemWeather = Project.FindSubsystem<SubsystemWeather>(true)!;
        _componentPlayer = Entity.FindComponent<ComponentPlayer>(true)!;
        Food = valuesDictionary.GetValue<float>("Food");
        Stamina = valuesDictionary.GetValue<float>("Stamina");
        Sleep = valuesDictionary.GetValue<float>("Sleep");
        Temperature = valuesDictionary.GetValue<float>("Temperature");
        Wetness = valuesDictionary.GetValue<float>("Wetness");
        _lastFood = Food;
        _lastStamina = Stamina;
        _lastSleep = Sleep;
        _lastTemperature = Temperature;
        _lastWetness = Wetness;
        _environmentTemperature = Temperature;
        if (RunMode.Value is RunModeType.Gui)
        {
            _pantingSound = _subsystemAudio.CreateSound("Audio/HumanPanting");
            _pantingSound.IsLooped = true;
        }

        foreach (var item in valuesDictionary.GetValue<ValuesDictionary>("Satiation"))
        {
            _satiation[int.Parse(item.Key, CultureInfo.InvariantCulture)] = (float)item.Value;
        }

        _componentPlayer.ComponentHealth.Attacked += delegate { _lastAttackedTime = _subsystemTime.GameTime; };
    }

    public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
    {
        valuesDictionary.SetValue("Food", Food);
        valuesDictionary.SetValue("Stamina", Stamina);
        valuesDictionary.SetValue("Sleep", Sleep);
        valuesDictionary.SetValue("Temperature", Temperature);
        valuesDictionary.SetValue("Wetness", Wetness);
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("Satiation", valuesDictionary2);
        foreach (var item in _satiation.Where(item => item.Value > 0f))
        {
            valuesDictionary2.SetValue(item.Key.ToString(CultureInfo.InvariantCulture), item.Value);
        }
    }

    public override void OnEntityRemoved()
    {
        DisposeSound();
    }

    public override void Dispose()
    {
        base.Dispose();
        DisposeSound();
    }

    public void UpdateFood(bool runGui = true)
    {
        var gameTimeDelta = _subsystemTime.GameTimeDelta;
        var num = _componentPlayer.ComponentLocomotion.LastWalkOrder?.Length() ?? 0f;
        var lastJumpOrder = _componentPlayer.ComponentLocomotion.LastJumpOrder;
        var num2 = _componentPlayer.ComponentCreatureModel.EyePosition.Y - _componentPlayer.ComponentBody.Position.Y;
        var flag = _componentPlayer.ComponentBody.ImmersionDepth > num2;
        var flag2 = _componentPlayer.ComponentBody is { ImmersionFactor: > 0.33f, StandingOnValue: null };
        var flag3 = _subsystemTime.PeriodicGameTimeEvent(240.0, 13.0) && !_componentPlayer.ComponentSickness.IsSick;
        if (_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative &&
            _subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
        {
            var hungerFactor = _componentPlayer.ComponentLevel.HungerFactor;
            //开服后，睡觉饥饿速度加快5倍
            if (CommonLib.WorkType == WorkType.Server &&
                _componentPlayer.ComponentSleep.SubsystemUpdate.UpdatesPerFrame == 1 &&
                _componentPlayer.ComponentSleep.IsSleeping)
            {
                hungerFactor *= _subsystemGameInfo.WorldSettings.RecoverFactor;
            }

            Food -= hungerFactor * gameTimeDelta / 2880f;
            if (flag2 | flag)
            {
                Food -= hungerFactor * gameTimeDelta * num / 1440f;
            }
            else
            {
                Food -= hungerFactor * gameTimeDelta * num / 2880f;
            }

            Food -= hungerFactor * lastJumpOrder / 1200f;
            if (_componentPlayer.ComponentMiner.DigCellFace.HasValue)
            {
                Food -= hungerFactor * gameTimeDelta / 2880f;
            }

            if (!_componentPlayer.ComponentSleep.IsSleeping)
            {
                if (Food <= 0f)
                {
                    if (_subsystemTime.PeriodicGameTimeEvent(50.0, 0.0))
                    {
                        _componentPlayer.ComponentHealth.Injure(0.05f, null, false, LanguageManager.Get(_typeName, 9));
                        if (runGui)
                        {
                            _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 10),
                                Color.White, true, false);
                            _componentPlayer.ComponentGui.FoodBarWidget.Flash(10);
                        }
                    }
                }
                else
                {
                    switch (runGui)
                    {
                        case true when Food < 0.1f && ((_lastFood >= 0.1f) | flag3):
                            _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 11),
                                Color.White,
                                true, true);
                            break;
                        case true when Food < 0.25f && ((_lastFood >= 0.25f) | flag3):
                            _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 12),
                                Color.White,
                                true, true);
                            break;
                        case true when Food < 0.5f && ((_lastFood >= 0.5f) | flag3):
                            _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 13),
                                Color.White,
                                true, false);
                            break;
                    }
                }
            }
        }
        else
        {
            Food = 0.9f;
        }

        if (_subsystemTime.PeriodicGameTimeEvent(1.0, -0.01))
        {
            _satiationList.Clear();
            _satiationList.AddRange(_satiation);
            _satiation.Clear();
            foreach (var satiation in _satiationList)
            {
                var num3 = MathUtils.Max(satiation.Value - 0.000416666677f, 0f);
                if (num3 > 0f)
                {
                    _satiation.Add(satiation.Key, num3);
                }
            }
        }

        _lastFood = Food;
        if (runGui)
        {
            _componentPlayer.ComponentGui.FoodBarWidget.Value = Food;
        }
    }

    public void UpdateStamina()
    {
        var gameTimeDelta = _subsystemTime.GameTimeDelta;
        var num = _componentPlayer.ComponentLocomotion.LastWalkOrder?.Length() ?? 0f;
        var lastJumpOrder = _componentPlayer.ComponentLocomotion.LastJumpOrder;
        var num2 = _componentPlayer.ComponentCreatureModel.EyePosition.Y - _componentPlayer.ComponentBody.Position.Y;
        var flag = _componentPlayer.ComponentBody.ImmersionDepth > num2;
        var flag2 = _componentPlayer.ComponentBody is { ImmersionFactor: > 0.33f, StandingOnValue: null };
        if (_subsystemGameInfo.WorldSettings is
            {
                GameMode: >= GameMode.Survival,
                AreAdventureSurvivalMechanicsEnabled: true
            })
        {
            var num3 = 1f / MathUtils.Max(_componentPlayer.ComponentLevel.SpeedFactor, 0.75f);
            if (_componentPlayer.ComponentSickness.IsSick || _componentPlayer.ComponentFlu.HasFlu)
            {
                num3 *= 5f;
            }

            Stamina += gameTimeDelta * 0.07f;
            Stamina -= 0.025f * lastJumpOrder * num3;
            if (flag2 | flag)
            {
                Stamina -= gameTimeDelta * (0.07f + 0.006f * num3 + 0.008f * num);
            }
            else
            {
                Stamina -= gameTimeDelta * (0.07f + 0.006f * num3) * num;
            }

            if (!flag2 && !flag && Stamina < 0.33f && _lastStamina >= 0.33f)
            {
                _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 14), Color.White, true,
                    false);
            }

            if (flag2 | flag && Stamina < 0.4f && _lastStamina >= 0.4f)
            {
                _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 15), Color.White, true,
                    true);
            }

            if (Stamina < 0.1f)
            {
                if (flag2 | flag)
                {
                    if (_subsystemTime.PeriodicGameTimeEvent(5.0, 0.0))
                    {
                        _componentPlayer.ComponentHealth.Injure(0.05f, null, false, LanguageManager.Get(_typeName, 16));
                        _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 17),
                            Color.White,
                            true, false);
                    }

                    if (_random.Float(0f, 1f) < 1f * gameTimeDelta)
                    {
                        _componentPlayer.ComponentLocomotion.JumpOrder = 1f;
                    }
                }
                else if (_subsystemTime.PeriodicGameTimeEvent(5.0, 0.0))
                {
                    _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 18), Color.White,
                        true, true);
                }
            }

            _lastStamina = Stamina;
            var num4 = MathUtils.Saturate(2f * (0.5f - Stamina));
            if (!flag && num4 > 0f)
            {
                if (_pantingSound == null)
                {
                    _pantingSound = _subsystemAudio.CreateSound("Audio/HumanPanting");
                    _pantingSound.IsLooped = true;
                }

                var num5 = _componentPlayer.PlayerData.PlayerClass == PlayerClass.Female ? 0.2f : 0f;
                _pantingSound.Volume = 1f * SettingsManager.SoundsVolume * MathUtils.Saturate(1f * num4) *
                                       MathUtils.Lerp(0.8f, 1f,
                                           SimplexNoise.Noise((float)MathUtils.Remainder(3.0 * Time.RealTime + 100.0,
                                               1000.0)));
                _pantingSound.Pitch = AudioManager.ToEnginePitch(num5 + MathUtils.Lerp(-0.15f, 0.05f, num4) *
                    MathUtils.Lerp(0.8f, 1.2f,
                        SimplexNoise.Noise((float)MathUtils.Remainder(3.0 * Time.RealTime + 200.0, 1000.0))));
                _pantingSound.Play();
            }
            else
            {
                DisposeSound();
            }

            var num6 = MathUtils.Saturate(3f * (0.33f - Stamina));
            if (num6 > 0f && SimplexNoise.Noise((float)MathUtils.Remainder(Time.RealTime, 1000.0)) < num6)
            {
                ApplyDensityModifier(0.6f);
            }
            else
            {
                ApplyDensityModifier(0f);
            }
        }
        else
        {
            Stamina = 1f;
            ApplyDensityModifier(0f);
            DisposeSound();
        }
    }

    public void UpdateSleep(bool runGui = true)
    {
        var gameTimeDelta = _subsystemTime.GameTimeDelta;
        var flag = _componentPlayer.ComponentBody.ImmersionFactor > 0.05f;
        var flag2 = _subsystemTime.PeriodicGameTimeEvent(240.0, 9.0);
        if (_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative &&
            _subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
        {
            if (_componentPlayer.ComponentSleep.SleepFactor.CloseTo(1f))
            {
                Sleep += 0.05f * gameTimeDelta;
            }
            else if (!flag && (!_lastAttackedTime.HasValue || _subsystemTime.GameTime - _lastAttackedTime > 10.0))
            {
                var amount = gameTimeDelta / 1800f;
                Sleep -= amount;
                switch (runGui)
                {
                    case true when Sleep < 0.075f && (_lastSleep >= 0.075f) | flag2:
                        _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 19),
                            Color.White,
                            true, true);
                        _componentPlayer.ComponentCreatureSounds.PlayMoanSound();
                        break;
                    case true when Sleep < 0.2f && (_lastSleep >= 0.2f) | flag2:
                        _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 20),
                            Color.White,
                            true, true);
                        _componentPlayer.ComponentCreatureSounds.PlayMoanSound();
                        break;
                    case true when Sleep < 0.33f && (_lastSleep >= 0.33f) | flag2:
                        _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 21),
                            Color.White,
                            true, false);
                        break;
                    case true when Sleep < 0.5f && (_lastSleep >= 0.5f) | flag2:
                        _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 22),
                            Color.White,
                            true, false);
                        break;
                }

                if (runGui && Sleep < 0.075f)
                {
                    var num = MathUtils.Lerp(0.05f, 0.2f, (0.075f - Sleep) / 0.075f);
                    var x = Sleep < 0.0375f ? _random.Float(3f, 6f) : _random.Float(2f, 4f);
                    if (_random.Float(0f, 1f) < num * gameTimeDelta)
                    {
                        _sleepBlackoutDuration = MathUtils.Max(_sleepBlackoutDuration, x);
                        _componentPlayer.ComponentCreatureSounds.PlayMoanSound();
                    }
                }

                if (Sleep <= 0f && !_componentPlayer.ComponentSleep.IsSleeping)
                {
                    _componentPlayer.ComponentSleep.Sleep(false);
                    if (runGui)
                    {
                        _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 23),
                            Color.White, true, true);
                        _componentPlayer.ComponentCreatureSounds.PlayMoanSound();
                    }
                }
            }
        }
        else
        {
            Sleep = 0.9f;
        }

        _lastSleep = Sleep;
        if (!runGui)
        {
            return;
        }

        _sleepBlackoutDuration -= gameTimeDelta;
        var num2 = MathUtils.Saturate(0.5f * _sleepBlackoutDuration);
        _sleepBlackoutFactor =
            MathUtils.Saturate(_sleepBlackoutFactor + 2f * gameTimeDelta * (num2 - _sleepBlackoutFactor));
        if (_componentPlayer.ComponentSleep.IsSleeping)
        {
            return;
        }

        _componentPlayer.ComponentScreenOverlays.BlackoutFactor = MathUtils.Max(_sleepBlackoutFactor,
            _componentPlayer.ComponentScreenOverlays.BlackoutFactor);
        if (!(_sleepBlackoutFactor > 0.01))
        {
            return;
        }

        _componentPlayer.ComponentScreenOverlays.FloatingMessage = LanguageManager.Get(_typeName, 24);
        _componentPlayer.ComponentScreenOverlays.FloatingMessageFactor =
            MathUtils.Saturate(10f * (_sleepBlackoutFactor - 0.9f));
    }

    public void UpdateTemperature(bool runGui = true)
    {
        var gameTimeDelta = _subsystemTime.GameTimeDelta;
        var flag = _subsystemTime.PeriodicGameTimeEvent(300.0, 17.0);
        var num = _componentPlayer.ComponentClothing.Insulation *
                  MathUtils.Lerp(1f, 0.05f, MathUtils.Saturate(4f * Wetness));
        if (_subsystemGameInfo.WorldSettings.GameMode <= GameMode.Survival)
        {
            num = num * 1.5f + 1f;
        }

        var arg = _componentPlayer.ComponentClothing.LeastInsulatedSlot switch
        {
            ClothingSlot.Head => LanguageManager.Get(_typeName, 41),
            ClothingSlot.Torso => LanguageManager.Get(_typeName, 42),
            ClothingSlot.Legs => LanguageManager.Get(_typeName, 43),
            _ => LanguageManager.Get(_typeName, 44)
        };

        if (_subsystemTime.PeriodicGameTimeEvent(2.0, 2.0 * GetHashCode() % 1000.0 / 1000.0))
        {
            var x = Terrain.ToCell(_componentPlayer.ComponentBody.Position.X);
            var y = Terrain.ToCell(_componentPlayer.ComponentBody.Position.Y + 0.1f);
            var z = Terrain.ToCell(_componentPlayer.ComponentBody.Position.Z);
            _subsystemMetersBlockBehavior.CalculateTemperature(x, y, z, 12f, num, out _environmentTemperature,
                out _environmentTemperatureFlux);
        }

        if (_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative &&
            _subsystemGameInfo.WorldSettings.AreAdventureSurvivalMechanicsEnabled)
        {
            var num2 = _environmentTemperature - Temperature;
            var num3 = 0.01f + 0.005f * _environmentTemperatureFlux;
            var numOrigin = _componentPlayer.ComponentClothing.Insulation;
            if (num2 < 0)
            {
                switch (numOrigin)
                {
                    case >= 6f:
                        num3 *= 0.1f;
                        break;
                    case > 4f:
                        num3 *= 0.25f;
                        break;
                }
            }

            Temperature += MathUtils.Saturate(num3 * gameTimeDelta) * num2;
        }
        else
        {
            Temperature = 12f;
        }

        switch (Temperature)
        {
            case <= 0f:
                _componentPlayer.ComponentHealth.Injure(1f, null, false, LanguageManager.Get(_typeName, 25));
                break;
            case < 3f:
            {
                if (_subsystemTime.PeriodicGameTimeEvent(10.0, 0.0))
                {
                    _componentPlayer.ComponentHealth.Injure(0.05f, null, false, LanguageManager.Get(_typeName, 26));
                    string text;

                    if (Wetness > 0f)
                    {
                        text = string.Format(LanguageManager.Get(_typeName, 27), arg); // 你的{0}冻僵了,弄干你的衣服,
                    }
                    else if (num >= 1f) // 有衣服，但依然受冻的时候
                    {
                        text = string.Format(LanguageManager.Get(_typeName, 28), arg); // 你的{0}冻僵了,寻找庇护所,
                    }
                    else
                    {
                        text = string.Format(LanguageManager.Get(_typeName, 29), arg); // 你的{0}冻僵了,快穿上衣服,
                    }

                    if (runGui)
                    {
                        _componentPlayer.ComponentGui.DisplaySmallMessage(text, Color.White, true, false);
                        _componentPlayer.ComponentGui.TemperatureBarWidget.Flash(10);
                    }
                }

                break;
            }
            default:
            {
                switch (runGui)
                {
                    // 当体温低于 6 时
                    case true when Temperature < 6f && (_lastTemperature >= 6f) | flag:
                    {
                        var text2 = Wetness switch
                        {
                            > 0f => string.Format(LanguageManager.Get(_typeName, 30), arg), // 你的{0}有点冷, 弄干你的衣服
                            _ => string.Format(num >= 1f
                                ? LanguageManager.Get(_typeName, 31) // 你的{0}有点冷,寻找庇护所
                                : LanguageManager.Get(_typeName, 32), arg) // 你的{0}有点冷,快穿上衣服
                        };

                        _componentPlayer.ComponentGui.DisplaySmallMessage(text2, Color.White, true, true);
                        _componentPlayer.ComponentGui.TemperatureBarWidget.Flash(10);
                        break;
                    }
                    // 当体温低于 8 时： 你觉得有点冷
                    case true when Temperature < 8f && (_lastTemperature >= 8f) | flag:
                        _componentPlayer.ComponentGui.DisplaySmallMessage(
                            LanguageManager.Get(_typeName, 33),
                            Color.White,
                            true,
                            false
                        );
                        _componentPlayer.ComponentGui.TemperatureBarWidget.Flash(10);
                        break;
                }

                break;
            }
        }

        if (Temperature >= 24f) // 体温大于24
        {
            if (_subsystemTime.PeriodicGameTimeEvent(10.0, 0.0))
            {
                _componentPlayer.ComponentHealth.Injure(0.05f, null, false, LanguageManager.Get(_typeName, 35)); // 被热死了
                if (runGui)
                {
                    // 这里非常热，快离开这里
                    _componentPlayer.ComponentGui.DisplaySmallMessage(
                        LanguageManager.Get(_typeName, 34),
                        Color.White,
                        true,
                        false
                    );
                    _componentPlayer.ComponentGui.TemperatureBarWidget.Flash(10);
                }
            }

            if (runGui && _subsystemTime.PeriodicGameTimeEvent(8.0, 0.0))
            {
                _temperatureBlackoutDuration = MathUtils.Max(_temperatureBlackoutDuration, 6f);
                _componentPlayer.ComponentCreatureSounds.PlayMoanSound();
            }
        }
        else if (runGui && Temperature > 20f && _subsystemTime.PeriodicGameTimeEvent(10.0, 0.0)) // 温度大于20
        {
            // 你觉得热
            _componentPlayer.ComponentGui.DisplaySmallMessage(
                LanguageManager.Get(_typeName, 36),
                Color.White,
                true,
                false
            );
            _temperatureBlackoutDuration = MathUtils.Max(_temperatureBlackoutDuration, 3f);
            _componentPlayer.ComponentGui.TemperatureBarWidget.Flash(10);
            _componentPlayer.ComponentCreatureSounds.PlayMoanSound();
        }

        _lastTemperature = Temperature;
        if (!runGui)
        {
            return;
        }

        _componentPlayer.ComponentScreenOverlays.IceFactor = MathUtils.Saturate(1f - Temperature / 6f);
        _temperatureBlackoutDuration -= gameTimeDelta;
        var num4 = MathUtils.Saturate(0.5f * _temperatureBlackoutDuration);
        _temperatureBlackoutFactor =
            MathUtils.Saturate(_temperatureBlackoutFactor + 2f * gameTimeDelta * (num4 - _temperatureBlackoutFactor));
        _componentPlayer.ComponentScreenOverlays.BlackoutFactor = MathUtils.Max(_temperatureBlackoutFactor,
            _componentPlayer.ComponentScreenOverlays.BlackoutFactor);
        if (_temperatureBlackoutFactor > 0.01)
        {
            _componentPlayer.ComponentScreenOverlays.FloatingMessage = LanguageManager.Get(_typeName, 37);
            _componentPlayer.ComponentScreenOverlays.FloatingMessageFactor =
                MathUtils.Saturate(10f * (_temperatureBlackoutFactor - 0.9f));
        }

        _componentPlayer.ComponentGui.TemperatureBarWidget.BarSubtexture = _environmentTemperature switch
        {
            > 22f => ContentManager.Get<Subtexture>("Textures/Atlas/Temperature6"),
            > 18f => ContentManager.Get<Subtexture>("Textures/Atlas/Temperature5"),
            > 14f => ContentManager.Get<Subtexture>("Textures/Atlas/Temperature4"),
            > 10f => ContentManager.Get<Subtexture>("Textures/Atlas/Temperature3"),
            > 6f => ContentManager.Get<Subtexture>("Textures/Atlas/Temperature2"),
            _ => _environmentTemperature > 2f
                ? ContentManager.Get<Subtexture>("Textures/Atlas/Temperature1")
                : ContentManager.Get<Subtexture>("Textures/Atlas/Temperature0")
        };
    }

    public void UpdateWetness(bool runGui = true)
    {
        var gameTimeDelta = _subsystemTime.GameTimeDelta;
        if (_componentPlayer.ComponentBody is { ImmersionFactor: > 0.2f, ImmersionFluidBlock: WaterBlock })
        {
            var num = 2f * _componentPlayer.ComponentBody.ImmersionFactor;
            Wetness += MathUtils.Saturate(3f * gameTimeDelta) * (num - Wetness);
        }

        var x = Terrain.ToCell(_componentPlayer.ComponentBody.Position.X);
        var num2 = Terrain.ToCell(_componentPlayer.ComponentBody.Position.Y + 0.1f);
        var z = Terrain.ToCell(_componentPlayer.ComponentBody.Position.Z);
        var precipitationShaftInfo = _subsystemWeather.GetPrecipitationShaftInfo(x, z);
        if (num2 >= precipitationShaftInfo.YLimit && precipitationShaftInfo.Type == PrecipitationType.Rain)
        {
            Wetness += 0.05f * precipitationShaftInfo.Intensity * gameTimeDelta;
        }

        var num3 = 180f;
        if (_environmentTemperature > 8f)
        {
            num3 = 120f;
        }

        if (_environmentTemperature > 16f)
        {
            num3 = 60f;
        }

        if (_environmentTemperature > 24f)
        {
            num3 = 30f;
        }

        Wetness -= gameTimeDelta / num3;
        switch (runGui)
        {
            case true when Wetness > 0.8f && _lastWetness <= 0.8f:
                Time.QueueTimeDelayedExecution(
                    Time.FrameStartTime + 2.0,
                    delegate
                    {
                        if (Wetness > 0.8f)
                        {
                            _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageManager.Get(_typeName, 38),
                                Color.White,
                                true, true);
                        }
                    });
                break;
            case true when Wetness > 0.2f && _lastWetness <= 0.2f:
                Time.QueueTimeDelayedExecution(
                    Time.FrameStartTime + 2.0,
                    delegate
                    {
                        if (Wetness is > 0.2f and <= 0.8f && Wetness > _lastWetness)
                        {
                            _componentPlayer.ComponentGui.DisplaySmallMessage(
                                LanguageManager.Get(_typeName, 39),
                                Color.White,
                                true,
                                true
                            );
                        }
                    });
                break;
            case true when Wetness <= 0f && _lastWetness > 0f:
                Time.QueueTimeDelayedExecution(
                    Time.FrameStartTime + 2.0,
                    delegate
                    {
                        if (Wetness <= 0f)
                        {
                            _componentPlayer.ComponentGui.DisplaySmallMessage(
                                LanguageManager.Get(_typeName, 40),
                                Color.White,
                                true,
                                true
                            );
                        }
                    });
                break;
        }

        _lastWetness = Wetness;
    }

    public void ApplyDensityModifier(float modifier)
    {
        var num = modifier - _densityModifierApplied;
        if (num == 0f)
        {
            return;
        }

        _densityModifierApplied = modifier;
        _componentPlayer.ComponentBody.Density += num;
    }
}
