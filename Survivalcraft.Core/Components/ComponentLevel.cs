using System.Globalization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Components;

public class ComponentLevel : Component, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    public const float FemaleStrengthFactor = 0.8f;

    public const float FemaleResilienceFactor = 0.8f;

    public const float FemaleSpeedFactor = 1.03f;

    public const float FemaleHungerFactor = 0.7f;

    public const string Name = "ComponentLevel";

    public event Action<int, bool>? ExperienceReceived;

    private ComponentCharacter _componentCharacter = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemTime _subsystemTime = null!;

    public float StrengthFactor { get; set; }

    public float ResilienceFactor { get; set; }

    public float SpeedFactor { get; set; }

    public float HungerFactor { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project)) { return; }

        if (WorldRuntimeContext.For(Project).IsAuthority && _subsystemTime.PeriodicGameTimeEvent(180.0, 179.0))
        {
            AddExperience(1, false);
        }

        StrengthFactor = CalculateStrengthFactor([]);
        SpeedFactor = CalculateSpeedFactor([]);
        HungerFactor = CalculateHungerFactor([]);
        ResilienceFactor = CalculateResilienceFactor([]);

        UpdateHighestLevel();
    }

    public void AddExperience(int count, bool playSound)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || !WorldRuntimeContext.For(Project).IsAuthority) { return; }

        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0) { return; }

        var previousLevel = _componentCharacter.CharacterData.Level;
        for (var i = 0; i < count; i++)
        {
            var amount = 0.012f / MathUtils.Pow(1.08f, MathUtils.Floor(_componentCharacter.CharacterData.Level - 1f));
            _componentCharacter.CharacterData.Level += amount;
        }

        var level = _componentCharacter.CharacterData.Level;
        var levelUps = (int)(MathUtils.Floor(level) - MathUtils.Floor(previousLevel));
        UpdateHighestLevel();
        NetworkSender.SendToObservers(
            Entity,
            CharacterPackageBuilder.Capture(_componentCharacter.CharacterData, levelUps, playSound, level)
        );
    }

    public void ApplyExperienceState(float level, int levelUps, bool playSound)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project) || WorldRuntimeContext.For(Project).IsAuthority) { return; }

        if (!float.IsFinite(level) || level < 1f || levelUps < 0 || levelUps > MathUtils.Floor(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        _componentCharacter.CharacterData.Level = level;
        UpdateHighestLevel();
        ExperienceReceived?.Invoke(levelUps, playSound);
    }

    private void UpdateHighestLevel()
    {
        _componentCharacter.CharacterStats.HighestLevel = MathUtils.Max(
            _componentCharacter.CharacterStats.HighestLevel,
            _componentCharacter.CharacterData.Level
        );
    }

    public float CalculateStrengthFactor(ICollection<Factor> factors)
    {
        var num = _componentCharacter.CharacterData.CharacterSex == CharacterSex.Female ? 0.8f : 1f;
        var num2 = 1f * num;
        Factor item;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num,
                Description = LocalizedText.Literal(_componentCharacter.CharacterData.CharacterSex.ToString())
            };
            factors.Add(item);
        }

        var level = _componentCharacter.CharacterData.Level;
        var num3 = 1f + 0.05f * MathUtils.Floor(MathUtils.Clamp(level, 1f, 21f) - 1f);
        var num4 = num2 * num3;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num3,
                Description = new LocalizedText(Name, "2", string.Empty),
                DescriptionArguments = [MathUtils.Floor(level).ToString(CultureInfo.CurrentCulture)]
            };
            factors.Add(item);
        }

        var stamina = _componentCharacter.ComponentVitalStats.Stamina;
        var num5 = MathUtils.Lerp(0.5f, 1f, MathUtils.Saturate(4f * stamina)) *
                   MathUtils.Lerp(0.9f, 1f, MathUtils.Saturate(stamina));
        var num6 = num4 * num5;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num5,
                Description = new LocalizedText(Name, "3", string.Empty),
                DescriptionArguments = [$"{stamina * 100f:0}"]
            };
            factors.Add(item);
        }

        var num7 = _componentCharacter.ComponentSickness.IsSick ? 0.75f : 1f;
        var num8 = num6 * num7;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num7,
                Description = _componentCharacter.ComponentSickness.IsSick
                    ? new LocalizedText(Name, "4", string.Empty)
                    : new LocalizedText(Name, "5", string.Empty)
            };
            factors.Add(item);
        }

        float num9 = !_componentCharacter.ComponentSickness.IsPuking ? 1 : 0;
        var num10 = num8 * num9;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num9,
                Description = _componentCharacter.ComponentSickness.IsPuking
                    ? new LocalizedText(Name, "6", string.Empty)
                    : new LocalizedText(Name, "7", string.Empty)
            };
            factors.Add(item);
        }

        var num11 = _componentCharacter.ComponentFlu.HasFlu ? 0.75f : 1f;
        var num12 = num10 * num11;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num11,
                Description = _componentCharacter.ComponentFlu.HasFlu
                    ? new LocalizedText(Name, "8", string.Empty)
                    : new LocalizedText(Name, "9", string.Empty)
            };
            factors.Add(item);
        }

        float num13 = !_componentCharacter.ComponentFlu.IsCoughing ? 1 : 0;
        var num14 = num12 * num13;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num13,
                Description = _componentCharacter.ComponentFlu.IsCoughing
                    ? new LocalizedText(Name, "10", string.Empty)
                    : new LocalizedText(Name, "11", string.Empty)
            };
            factors.Add(item);
        }

        var num15 = _subsystemGameInfo.WorldSettings.GameMode == GameMode.Harmless ? 1.25f : 1f;
        var result = num14 * num15;

        if (factors.Count <= 0)
        {
            return result;
        }

        item = new Factor
        {
            Value = num15,
            Description = new LocalizedText(Name, "12", string.Empty),
            DescriptionArguments = [_subsystemGameInfo.WorldSettings.GameMode.ToString()]
        };
        factors.Add(item);

        return result;
    }

    public float CalculateResilienceFactor(ICollection<Factor> factors)
    {
        var num = _componentCharacter.CharacterData.CharacterSex == CharacterSex.Female ? 0.8f : 1f;
        var num2 = 1f * num;
        Factor item;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num,
                Description = LocalizedText.Literal(_componentCharacter.CharacterData.CharacterSex.ToString())
            };
            factors.Add(item);
        }

        var level = _componentCharacter.CharacterData.Level;
        var num3 = 1f + 0.05f * MathUtils.Floor(MathUtils.Clamp(level, 1f, 21f) - 1f);
        var num4 = num2 * num3;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num3,
                Description = new LocalizedText(Name, "2", string.Empty),
                DescriptionArguments = [MathUtils.Floor(level).ToString(CultureInfo.CurrentCulture)]
            };
            factors.Add(item);
        }

        var num5 = _componentCharacter.ComponentSickness.IsSick ? 0.75f : 1f;
        var num6 = num4 * num5;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num5,
                Description = _componentCharacter.ComponentSickness.IsSick
                    ? new LocalizedText(Name, "4", string.Empty)
                    : new LocalizedText(Name, "5", string.Empty)
            };
            factors.Add(item);
        }

        var num7 = _componentCharacter.ComponentFlu.HasFlu ? 0.75f : 1f;
        var num8 = num6 * num7;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num7,
                Description = _componentCharacter.ComponentFlu.HasFlu
                    ? new LocalizedText(Name, "8", string.Empty)
                    : new LocalizedText(Name, "9", string.Empty)
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

        if (factors.Count <= 0)
        {
            return result;
        }

        item = new Factor
        {
            Value = num9,
            Description = new LocalizedText(Name, "12", string.Empty),
            DescriptionArguments = [_subsystemGameInfo.WorldSettings.GameMode.ToString()]
        };
        factors.Add(item);

        return result;
    }

    public float CalculateSpeedFactor(ICollection<Factor> factors)
    {
        var num = 1f;
        var num2 = _componentCharacter.CharacterData.CharacterSex == CharacterSex.Female ? 1.03f : 1f;
        num *= num2;
        Factor item;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num2,
                Description = LocalizedText.Literal(_componentCharacter.CharacterData.CharacterSex.ToString())
            };
            factors.Add(item);
        }

        var level = _componentCharacter.CharacterData.Level;
        var num3 = 1f + 0.02f * MathUtils.Floor(MathUtils.Clamp(level, 1f, 21f) - 1f);
        num *= num3;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num3,
                Description = new LocalizedText(Name, "2", string.Empty),
                DescriptionArguments = [MathUtils.Floor(level).ToString(CultureInfo.CurrentCulture)]
            };
            factors.Add(item);
        }

        var clothingFactor = 1f;
        foreach (var clothe in _componentCharacter.ComponentClothing.GetClothes(ClothingSlot.Head))
        {
            AddClothingFactor(clothe, ref clothingFactor, factors);
        }

        foreach (var clothe2 in _componentCharacter.ComponentClothing.GetClothes(ClothingSlot.Torso))
        {
            AddClothingFactor(clothe2, ref clothingFactor, factors);
        }

        foreach (var clothe3 in _componentCharacter.ComponentClothing.GetClothes(ClothingSlot.Legs))
        {
            AddClothingFactor(clothe3, ref clothingFactor, factors);
        }

        foreach (var clothe4 in _componentCharacter.ComponentClothing.GetClothes(ClothingSlot.Feet))
        {
            AddClothingFactor(clothe4, ref clothingFactor, factors);
        }

        num *= clothingFactor;
        var stamina = _componentCharacter.ComponentVitalStats.Stamina;
        var num4 = MathUtils.Lerp(0.5f, 1f, MathUtils.Saturate(4f * stamina)) *
                   MathUtils.Lerp(0.9f, 1f, MathUtils.Saturate(stamina));
        num *= num4;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num4,
                Description = new LocalizedText(Name, "3", string.Empty),
                DescriptionArguments = [$"{stamina * 100f:0}"]
            };
            factors.Add(item);
        }

        var num5 = _componentCharacter.ComponentSickness.IsSick ? 0.75f : 1f;
        num *= num5;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num5,
                Description = _componentCharacter.ComponentSickness.IsSick
                    ? new LocalizedText(Name, "4", string.Empty)
                    : new LocalizedText(Name, "5", string.Empty)
            };
            factors.Add(item);
        }

        float num6 = !_componentCharacter.ComponentSickness.IsPuking ? 1 : 0;
        num *= num6;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num6,
                Description = _componentCharacter.ComponentSickness.IsPuking
                    ? new LocalizedText(Name, "6", string.Empty)
                    : new LocalizedText(Name, "7", string.Empty)
            };
            factors.Add(item);
        }

        var num7 = _componentCharacter.ComponentFlu.HasFlu ? 0.75f : 1f;
        num *= num7;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num7,
                Description = _componentCharacter.ComponentFlu.HasFlu
                    ? new LocalizedText(Name, "8", string.Empty)
                    : new LocalizedText(Name, "9", string.Empty)
            };
            factors.Add(item);
        }

        float num8 = !_componentCharacter.ComponentFlu.IsCoughing ? 1 : 0;
        num *= num8;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num8,
                Description = _componentCharacter.ComponentFlu.IsCoughing
                    ? new LocalizedText(Name, "10", string.Empty)
                    : new LocalizedText(Name, "11", string.Empty)
            };
            factors.Add(item);
        }

        return num;
    }

    public float CalculateHungerFactor(ICollection<Factor> factors)
    {
        var num = _componentCharacter.CharacterData.CharacterSex == CharacterSex.Female ? 0.7f : 1f;
        var num2 = 1f * num;
        Factor item;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num,
                Description = LocalizedText.Literal(_componentCharacter.CharacterData.CharacterSex.ToString())
            };
            factors.Add(item);
        }

        var level = _componentCharacter.CharacterData.Level;
        var num3 = 1f - 0.01f * MathUtils.Floor(MathUtils.Clamp(level, 1f, 21f) - 1f);
        var num4 = num2 * num3;
        if (factors.Count > 0)
        {
            item = new Factor
            {
                Value = num3,
                Description = new LocalizedText(Name, "2", string.Empty),
                DescriptionArguments = [MathUtils.Floor(level).ToString(CultureInfo.CurrentCulture)]
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

        if (factors.Count <= 0)
        {
            return result;
        }

        item = new Factor
        {
            Value = num5,
            Description = new LocalizedText(Name, "12", string.Empty),
            DescriptionArguments = [_subsystemGameInfo.WorldSettings.GameMode.ToString()]
        };
        factors.Add(item);

        return result;
    }

    public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _componentCharacter = Entity.FindComponent<ComponentCharacter>(true)!;
        StrengthFactor = 1f;
        SpeedFactor = 1f;
        HungerFactor = 1f;
        ResilienceFactor = 1f;
    }

    private void AddClothingFactor(int clothingValue, ref float clothingFactor, ICollection<Factor> factors)
    {
        var clothingData = WorldContentBinding.For(Project).Catalog
            .Blocks[Terrain.ExtractContents(clothingValue)]
            .GetClothingData(Terrain.ExtractData(clothingValue));
        if (clothingData.MovementSpeedFactor.CloseTo(1f))
        {
            return;
        }

        clothingFactor *= clothingData.MovementSpeedFactor;
        factors.Add(
            new Factor
            {
                Value = clothingData.MovementSpeedFactor,
                Description = LocalizedText.Empty,
                ClothingValue = clothingValue
            }
        );
    }

    public struct Factor
    {
        public LocalizedText? Description;

        public string[]? DescriptionArguments;

        public int? ClothingValue;

        public float Value;
    }
}
