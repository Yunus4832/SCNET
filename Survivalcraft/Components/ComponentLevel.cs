using System.Globalization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Components;

public class ComponentLevel : Component, IUpdateable
{
    public const float FemaleStrengthFactor = 0.8f;

    public const float FemaleResilienceFactor = 0.8f;

    public const float FemaleSpeedFactor = 1.03f;

    public const float FemaleHungerFactor = 0.7f;

    public const string Name = "ComponentLevel";

    //简单防内存修改
    private readonly SafeFloat _sw = new();

    private readonly SafeFloat _sx = new();

    private readonly SafeFloat _sy = new();

    private readonly SafeFloat _sz = new();

    private ComponentPlayer _componentPlayer = null!;

    private readonly List<Factor> _factors = [];

    private float? _lastLevelTextValue;

    private readonly Random _random = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTime _subsystemTime = null!;

    public float StrengthFactor
    {
        get => _sx.Get();
        set => _sx.Set(value);
    }

    public float ResilienceFactor
    {
        get => _sy.Get();
        set => _sy.Set(value);
    }

    public float SpeedFactor
    {
        get => _sz.Get();
        set => _sz.Set(value);
    }

    public float HungerFactor
    {
        get => _sw.Get();
        set => _sw.Set(value);
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_subsystemTime.PeriodicGameTimeEvent(180.0, 179.0))
        {
            AddExperience(1, false);
        }

        StrengthFactor = CalculateStrengthFactor([]);
        SpeedFactor = CalculateSpeedFactor([]);
        HungerFactor = CalculateHungerFactor([]);
        ResilienceFactor = CalculateResilienceFactor([]);
        if (RunMode.Value is RunModeType.Gui)
        {
            if (!_lastLevelTextValue.HasValue ||
                _lastLevelTextValue.Value.UncloseTo(MathUtils.Floor(_componentPlayer.PlayerData.Level)))
            {
                _componentPlayer.ComponentGui.LevelLabelWidget.Text =
                    "等级 " + MathUtils.Floor(_componentPlayer.PlayerData.Level);
                _lastLevelTextValue = MathUtils.Floor(_componentPlayer.PlayerData.Level);
            }
        }

        _componentPlayer.PlayerStats.HighestLevel = MathUtils.Max(_componentPlayer.PlayerStats.HighestLevel,
            _componentPlayer.PlayerData.Level);
        ModsManager.HookAction("OnLevelUpdate", modLoader =>
        {
            modLoader.OnLevelUpdate(this);
            return false;
        });
    }

    public void AddExperience(int count, bool playSound)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        NetAddExperience(count, playSound);
        CommonLib.Net.QueuePackage(new ComponentPlayerPackage(_componentPlayer.PlayerData, count, playSound,
            _componentPlayer.PlayerData.Level));
    }


    public void NetAddExperience(int count, bool playSound)
    {
        if (playSound)
        {
            _subsystemAudio.PlaySound("Audio/ExperienceCollected", 0.2f, _random.Float(-0.1f, 0.4f),
                _componentPlayer.ComponentBody.Position, 2f, false);
        }

        for (var i = 0; i < count; i++)
        {
            var num = 0.012f / MathUtils.Pow(1.08f, MathUtils.Floor(_componentPlayer.PlayerData.Level - 1f));
            if (MathUtils.Floor(_componentPlayer.PlayerData.Level + num) >
                MathUtils.Floor(_componentPlayer.PlayerData.Level))
            {
                Time.QueueTimeDelayedExecution(Time.FrameStartTime + 0.5 + 0.0,
                    delegate
                    {
                        _componentPlayer.ComponentGui.DisplaySmallMessage(LanguageControl.Get(Name, 1), Color.White,
                            true, false);
                    });
                Time.QueueTimeDelayedExecution(Time.FrameStartTime + 0.5 + 0.0,
                    delegate
                    {
                        _subsystemAudio.PlaySound("Audio/ExperienceCollected", 1f, -0.2f,
                            _componentPlayer.ComponentBody.Position, 2f, false);
                    });
                Time.QueueTimeDelayedExecution(Time.FrameStartTime + 0.5 + 0.15000000596046448,
                    delegate
                    {
                        _subsystemAudio.PlaySound("Audio/ExperienceCollected", 1f, -0.03333333f,
                            _componentPlayer.ComponentBody.Position, 2f, false);
                    });
                Time.QueueTimeDelayedExecution(Time.FrameStartTime + 0.5 + 0.30000001192092896,
                    delegate
                    {
                        _subsystemAudio.PlaySound("Audio/ExperienceCollected", 1f, 142f / (339f * (float)Math.PI),
                            _componentPlayer.ComponentBody.Position, 2f, false);
                    });
                Time.QueueTimeDelayedExecution(Time.FrameStartTime + 0.5 + 0.45000001788139343,
                    delegate
                    {
                        _subsystemAudio.PlaySound("Audio/ExperienceCollected", 1f, 23f / 60f,
                            _componentPlayer.ComponentBody.Position, 2f, false);
                    });
                Time.QueueTimeDelayedExecution(Time.FrameStartTime + 0.5 + 0.75,
                    delegate
                    {
                        _subsystemAudio.PlaySound("Audio/ExperienceCollected", 1f, -0.03333333f,
                            _componentPlayer.ComponentBody.Position, 2f, false);
                    });
                Time.QueueTimeDelayedExecution(Time.FrameStartTime + 0.5 + 0.90000003576278687,
                    delegate
                    {
                        _subsystemAudio.PlaySound("Audio/ExperienceCollected", 1f, 23f / 60f,
                            _componentPlayer.ComponentBody.Position, 2f, false);
                    });
            }

            _componentPlayer.PlayerData.Level += num;
        }
    }

    public float CalculateStrengthFactor(ICollection<Factor> factors)
    {
        var num = _componentPlayer.PlayerData.PlayerClass == PlayerClass.Female ? 0.8f : 1f;
        var num2 = 1f * num;
        Factor item;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num,
                Description = _componentPlayer.PlayerData.PlayerClass.ToString()
            };
            factors.Add(item);
        }

        var level = _componentPlayer.PlayerData.Level;
        var num3 = 1f + 0.05f * MathUtils.Floor(MathUtils.Clamp(level, 1f, 21f) - 1f);
        var num4 = num2 * num3;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num3,
                Description = string.Format(LanguageControl.Get(Name, 2),
                    MathUtils.Floor(level).ToString(CultureInfo.CurrentCulture))
            };
            factors.Add(item);
        }

        var stamina = _componentPlayer.ComponentVitalStats.Stamina;
        var num5 = MathUtils.Lerp(0.5f, 1f, MathUtils.Saturate(4f * stamina)) *
                   MathUtils.Lerp(0.9f, 1f, MathUtils.Saturate(stamina));
        var num6 = num4 * num5;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num5,
                Description = string.Format(LanguageControl.Get(Name, 3), $"{stamina * 100f:0}")
            };
            factors.Add(item);
        }

        var num7 = _componentPlayer.ComponentSickness.IsSick ? 0.75f : 1f;
        var num8 = num6 * num7;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num7,
                Description = _componentPlayer.ComponentSickness.IsSick
                    ? LanguageControl.Get(Name, 4)
                    : LanguageControl.Get(Name, 5)
            };
            factors.Add(item);
        }

        float num9 = !_componentPlayer.ComponentSickness.IsPuking ? 1 : 0;
        var num10 = num8 * num9;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num9,
                Description = _componentPlayer.ComponentSickness.IsPuking
                    ? LanguageControl.Get(Name, 6)
                    : LanguageControl.Get(Name, 7)
            };
            factors.Add(item);
        }

        var num11 = _componentPlayer.ComponentFlu.HasFlu ? 0.75f : 1f;
        var num12 = num10 * num11;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num11,
                Description = _componentPlayer.ComponentFlu.HasFlu
                    ? LanguageControl.Get(Name, 8)
                    : LanguageControl.Get(Name, 9)
            };
            factors.Add(item);
        }

        float num13 = !_componentPlayer.ComponentFlu.IsCoughing ? 1 : 0;
        var num14 = num12 * num13;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num13,
                Description = _componentPlayer.ComponentFlu.IsCoughing
                    ? LanguageControl.Get(Name, 10)
                    : LanguageControl.Get(Name, 11)
            };
            factors.Add(item);
        }

        var num15 = _subsystemGameInfo.WorldSettings.GameMode == GameMode.Harmless ? 1.25f : 1f;
        var result = num14 * num15;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num15,
                Description = string.Format(LanguageControl.Get(Name, 12),
                    _subsystemGameInfo.WorldSettings.GameMode.ToString())
            };
            factors.Add(item);
        }

        return result;
    }

    public float CalculateResilienceFactor(ICollection<Factor> factors)
    {
        var num = _componentPlayer.PlayerData.PlayerClass == PlayerClass.Female ? 0.8f : 1f;
        var num2 = 1f * num;
        Factor item;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num,
                Description = _componentPlayer.PlayerData.PlayerClass.ToString()
            };
            factors.Add(item);
        }

        var level = _componentPlayer.PlayerData.Level;
        var num3 = 1f + 0.05f * MathUtils.Floor(MathUtils.Clamp(level, 1f, 21f) - 1f);
        var num4 = num2 * num3;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num3,
                Description = string.Format(LanguageControl.Get(Name, 2),
                    MathUtils.Floor(level).ToString(CultureInfo.CurrentCulture))
            };
            factors.Add(item);
        }

        var num5 = _componentPlayer.ComponentSickness.IsSick ? 0.75f : 1f;
        var num6 = num4 * num5;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num5,
                Description = _componentPlayer.ComponentSickness.IsSick
                    ? LanguageControl.Get(Name, 4)
                    : LanguageControl.Get(Name, 5)
            };
            factors.Add(item);
        }

        var num7 = _componentPlayer.ComponentFlu.HasFlu ? 0.75f : 1f;
        var num8 = num6 * num7;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num7,
                Description = _componentPlayer.ComponentFlu.HasFlu
                    ? LanguageControl.Get(Name, 8)
                    : LanguageControl.Get(Name, 9)
            };
            factors.Add(item);
        }

        var num9 = _subsystemGameInfo.WorldSettings.GameMode switch
        {
            GameMode.Harmless => 1.5f,
            GameMode.Survival => 1.25f,
            GameMode.Creative => float.PositiveInfinity,
            _ => 1f
        };

        var result = num8 * num9;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num9,
                Description = string.Format(LanguageControl.Get(Name, 12),
                    _subsystemGameInfo.WorldSettings.GameMode.ToString())
            };
            factors.Add(item);
        }

        return result;
    }

    public float CalculateSpeedFactor(ICollection<Factor> factors)
    {
        var num = 1f;
        var num2 = _componentPlayer.PlayerData.PlayerClass == PlayerClass.Female ? 1.03f : 1f;
        num *= num2;
        Factor item;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num2,
                Description = _componentPlayer.PlayerData.PlayerClass.ToString()
            };
            factors.Add(item);
        }

        var level = _componentPlayer.PlayerData.Level;
        var num3 = 1f + 0.02f * MathUtils.Floor(MathUtils.Clamp(level, 1f, 21f) - 1f);
        num *= num3;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num3,
                Description = string.Format(LanguageControl.Get(Name, 2),
                    MathUtils.Floor(level).ToString(CultureInfo.CurrentCulture))
            };
            factors.Add(item);
        }

        var clothingFactor = 1f;
        foreach (var clothe in _componentPlayer.ComponentClothing.GetClothes(ClothingSlot.Head))
        {
            AddClothingFactor(clothe, ref clothingFactor, factors);
        }

        foreach (var clothe2 in _componentPlayer.ComponentClothing.GetClothes(ClothingSlot.Torso))
        {
            AddClothingFactor(clothe2, ref clothingFactor, factors);
        }

        foreach (var clothe3 in _componentPlayer.ComponentClothing.GetClothes(ClothingSlot.Legs))
        {
            AddClothingFactor(clothe3, ref clothingFactor, factors);
        }

        foreach (var clothe4 in _componentPlayer.ComponentClothing.GetClothes(ClothingSlot.Feet))
        {
            AddClothingFactor(clothe4, ref clothingFactor, factors);
        }

        num *= clothingFactor;
        var stamina = _componentPlayer.ComponentVitalStats.Stamina;
        var num4 = MathUtils.Lerp(0.5f, 1f, MathUtils.Saturate(4f * stamina)) *
                   MathUtils.Lerp(0.9f, 1f, MathUtils.Saturate(stamina));
        num *= num4;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num4,
                Description = string.Format(LanguageControl.Get(Name, 3), $"{stamina * 100f:0}")
            };
            factors.Add(item);
        }

        var num5 = _componentPlayer.ComponentSickness.IsSick ? 0.75f : 1f;
        num *= num5;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num5,
                Description = _componentPlayer.ComponentSickness.IsSick
                    ? LanguageControl.Get(Name, 4)
                    : LanguageControl.Get(Name, 5)
            };
            factors.Add(item);
        }

        float num6 = !_componentPlayer.ComponentSickness.IsPuking ? 1 : 0;
        num *= num6;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num6,
                Description = _componentPlayer.ComponentSickness.IsPuking
                    ? LanguageControl.Get(Name, 6)
                    : LanguageControl.Get(Name, 7)
            };
            factors.Add(item);
        }

        var num7 = _componentPlayer.ComponentFlu.HasFlu ? 0.75f : 1f;
        num *= num7;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num7,
                Description = _componentPlayer.ComponentFlu.HasFlu
                    ? LanguageControl.Get(Name, 8)
                    : LanguageControl.Get(Name, 9)
            };
            factors.Add(item);
        }

        float num8 = !_componentPlayer.ComponentFlu.IsCoughing ? 1 : 0;
        num *= num8;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num8,
                Description = _componentPlayer.ComponentFlu.IsCoughing
                    ? LanguageControl.Get(Name, 10)
                    : LanguageControl.Get(Name, 11)
            };
            factors.Add(item);
        }

        return num;
    }

    public float CalculateHungerFactor(ICollection<Factor> factors)
    {
        var num = _componentPlayer.PlayerData.PlayerClass == PlayerClass.Female ? 0.7f : 1f;
        var num2 = 1f * num;
        Factor item;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num,
                Description = _componentPlayer.PlayerData.PlayerClass.ToString()
            };
            factors.Add(item);
        }

        var level = _componentPlayer.PlayerData.Level;
        var num3 = 1f - 0.01f * MathUtils.Floor(MathUtils.Clamp(level, 1f, 21f) - 1f);
        var num4 = num2 * num3;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num3,
                Description = string.Format(LanguageControl.Get(Name, 2),
                    MathUtils.Floor(level).ToString(CultureInfo.CurrentCulture))
            };
            factors.Add(item);
        }

        var num5 = _subsystemGameInfo.WorldSettings.GameMode switch
        {
            GameMode.Harmless => 0.66f,
            GameMode.Survival => 0.75f,
            GameMode.Creative => 0f,
            _ => 1f
        };
        var result = num4 * num5;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num5,
                Description = string.Format(LanguageControl.Get(Name, 12),
                    _subsystemGameInfo.WorldSettings.GameMode.ToString())
            };
            factors.Add(item);
        }

        return result;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _componentPlayer = Entity.FindComponent<ComponentPlayer>(true)!;
        StrengthFactor = 1f;
        SpeedFactor = 1f;
        HungerFactor = 1f;
        ResilienceFactor = 1f;
    }

    public static void AddClothingFactor(int clothingValue, ref float clothingFactor, ICollection<Factor> factors)
    {
        var clothingData = BlocksManager.Blocks[Terrain.ExtractContents(clothingValue)]
            .GetClothingData(Terrain.ExtractData(clothingValue));
        if (clothingData.MovementSpeedFactor.CloseTo(1f))
        {
            return;
        }

        clothingFactor *= clothingData.MovementSpeedFactor;
        factors.Add(new Factor
        {
            Value = clothingData.MovementSpeedFactor,
            Description = clothingData.DisplayName
        });
    }

    public struct Factor
    {
        public string Description;

        public float Value;
    }
}
