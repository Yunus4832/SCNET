using System.Xml;

using EntitySystem.TemplatesDatabase;

using Game.Terrains.Generation;

namespace Game.World;

public class WorldSettings
{
    public TerrainGeneratorSettings Generator { get; set; } = new(TerrainGeneratorDefinition.Standard);

    public const ushort MaximumOnlinePlayerCount = 32;

    public bool AreAdventureSurvivalMechanicsEnabled = true;

    public bool AreSeasonsChanging = true;

    public bool AreSupernaturalCreaturesEnabled = true;

    public bool AreWeatherEffectsEnabled = true;

    public string BlocksTextureName = string.Empty;

    public float CreativeDigTime = 0.2f;

    public float CreativeReach = 7.5f;

    public float DaySpeed = 1f;

    public string DisableBlocks = string.Empty;

    public EnvironmentBehaviorMode EnvironmentBehaviorMode;

    public GameMode GameMode = GameMode.Survival;

    public int CreativeHotbarSlotsCount = PlayerInventoryLayout.MaxHotbarSlotsCount;

    public int SurvivalHotbarSlotsCount = PlayerInventoryLayout.MinHotbarSlotsCount;

    public int CurrentHotbarSlotsCount
    {
        get => GameMode == GameMode.Creative ? CreativeHotbarSlotsCount : SurvivalHotbarSlotsCount;
        set
        {
            if (GameMode == GameMode.Creative)
            {
                CreativeHotbarSlotsCount = value;
            }
            else
            {
                SurvivalHotbarSlotsCount = value;
            }
        }
    }

    public bool IsAdventureRespawnAllowed = true;

    public bool IsFriendlyFireEnabled = true;

    public string KeywordBlocking = string.Empty;

    public ushort MaxOnlinePlayerCount { get; set => field = Math.Min(value, MaximumOnlinePlayerCount); } = 20;

    public string Name = string.Empty;

    public WorldPalette Palette = new();

    public bool RandomSpawnPosition;

    public float RecoverFactor = 1f;

    public string Seed = string.Empty;

    public StartingPositionMode StartingPositionMode;

    public TimeOfDayMode TimeOfDayMode;

    public float TimeOfYear = SeasonCalendar.MidSummer;

    public float YearDays = 24f;

    public void ResetOptionsForNonCreativeMode(bool creatingWorld)
    {
        if (creatingWorld && Generator.Definition.CreativeOnly)
        {
            var standard = new TerrainGeneratorSettings(TerrainGeneratorDefinition.Standard);
            foreach (var parameter in standard.Definition.Parameters)
            {
                if (Generator.Definition.Parameters.Any(p => p.Key == parameter.Key))
                {
                    standard.Set(parameter.Key, Generator.Get<object>(parameter.Key));
                }
            }

            Generator = standard;
        }

        EnvironmentBehaviorMode = EnvironmentBehaviorMode.Living;
        TimeOfDayMode = TimeOfDayMode.Changing;
        AreWeatherEffectsEnabled = true;
        IsAdventureRespawnAllowed = true;
        AreAdventureSurvivalMechanicsEnabled = true;
    }

    public void Load(ValuesDictionary valuesDictionary)
    {
        Name = valuesDictionary.GetValue<string>("WorldName");
        Seed = valuesDictionary.GetValue("WorldSeedString", string.Empty);
        GameMode = valuesDictionary.GetValue("GameMode", GameMode.Challenging);
        CreativeDigTime = valuesDictionary.GetValue(nameof(CreativeDigTime), 0.2f);
        CreativeReach = valuesDictionary.GetValue(nameof(CreativeReach), 7.5f);
        SurvivalHotbarSlotsCount = MathUtils.Clamp(
            valuesDictionary.GetValue(
                "SurvivalHotbarSlotsCount",
                PlayerInventoryLayout.MinHotbarSlotsCount
            ),
            PlayerInventoryLayout.MinHotbarSlotsCount,
            PlayerInventoryLayout.MaxHotbarSlotsCount
        );
        CreativeHotbarSlotsCount = MathUtils.Clamp(
            valuesDictionary.GetValue(
                "CreativeHotbarSlotsCount",
                PlayerInventoryLayout.MaxHotbarSlotsCount
            ),
            PlayerInventoryLayout.MinHotbarSlotsCount,
            PlayerInventoryLayout.MaxHotbarSlotsCount
        );
        EnvironmentBehaviorMode = valuesDictionary.GetValue("EnvironmentBehaviorMode", EnvironmentBehaviorMode.Living);
        TimeOfDayMode = valuesDictionary.GetValue("TimeOfDayMode", TimeOfDayMode.Changing);
        AreSeasonsChanging = valuesDictionary.GetValue("AreSeasonsChanging", true);
        YearDays = valuesDictionary.GetValue("YearDays", 24f);
        TimeOfYear = valuesDictionary.GetValue("TimeOfYear", SeasonCalendar.MidSummer);
        StartingPositionMode = valuesDictionary.GetValue("StartingPositionMode", StartingPositionMode.Easy);
        AreWeatherEffectsEnabled = valuesDictionary.GetValue("AreWeatherEffectsEnabled", true);
        IsAdventureRespawnAllowed = valuesDictionary.GetValue("IsAdventureRespawnAllowed", true);
        AreAdventureSurvivalMechanicsEnabled = valuesDictionary.GetValue("AreAdventureSurvivalMechanicsEnabled", true);
        AreSupernaturalCreaturesEnabled = valuesDictionary.GetValue("AreSupernaturalCreaturesEnabled", true);
        IsFriendlyFireEnabled = valuesDictionary.GetValue("IsFriendlyFireEnabled", true);
        Generator = TerrainGeneratorSettings.Load(valuesDictionary);
        KeywordBlocking = valuesDictionary.GetValue("KeywordBlocking", string.Empty);
        KeywordBlocking = XmlConvert.DecodeName(KeywordBlocking);
        DaySpeed = valuesDictionary.GetValue("DaySpeed", 1f);
        RecoverFactor = valuesDictionary.GetValue("RecoverFactor", 1f);
        MaxOnlinePlayerCount = valuesDictionary.GetValue("MaxOnlinePlayerCount", MaxOnlinePlayerCount);
        BlocksTextureName = valuesDictionary.GetValue("BlockTextureName", string.Empty);
        DisableBlocks = valuesDictionary.GetValue("DisableBlocks", DisableBlocks);
        Palette = new WorldPalette(valuesDictionary.GetValue("Palette", new ValuesDictionary()));
        RandomSpawnPosition = valuesDictionary.GetValue("RandomSpawnPosition", RandomSpawnPosition);
    }

    public void Save(ValuesDictionary valuesDictionary, bool liveModifiableParametersOnly)
    {
        valuesDictionary.SetValue("WorldName", Name);
        valuesDictionary.SetValue("GameMode", GameMode);
        valuesDictionary.SetValue(nameof(CreativeDigTime), CreativeDigTime);
        valuesDictionary.SetValue(nameof(CreativeReach), CreativeReach);
        valuesDictionary.SetValue("SurvivalHotbarSlotsCount", SurvivalHotbarSlotsCount);
        valuesDictionary.SetValue("CreativeHotbarSlotsCount", CreativeHotbarSlotsCount);
        valuesDictionary.SetValue("EnvironmentBehaviorMode", EnvironmentBehaviorMode);
        valuesDictionary.SetValue("TimeOfDayMode", TimeOfDayMode);
        valuesDictionary.SetValue("AreSeasonsChanging", AreSeasonsChanging);
        valuesDictionary.SetValue("YearDays", YearDays);
        valuesDictionary.SetValue("TimeOfYear", TimeOfYear);
        valuesDictionary.SetValue("RecoverFactor", RecoverFactor);
        valuesDictionary.SetValue("DaySpeed", DaySpeed);
        valuesDictionary.SetValue("AreWeatherEffectsEnabled", AreWeatherEffectsEnabled);
        valuesDictionary.SetValue("IsAdventureRespawnAllowed", IsAdventureRespawnAllowed);
        valuesDictionary.SetValue("AreAdventureSurvivalMechanicsEnabled", AreAdventureSurvivalMechanicsEnabled);
        valuesDictionary.SetValue("AreSupernaturalCreaturesEnabled", AreSupernaturalCreaturesEnabled);
        valuesDictionary.SetValue("IsFriendlyFireEnabled", IsFriendlyFireEnabled);
        valuesDictionary.SetValue("KeywordBlocking", XmlConvert.EncodeName(KeywordBlocking));
        if (!liveModifiableParametersOnly)
        {
            valuesDictionary.SetValue("WorldSeedString", Seed);
            Generator.Save(valuesDictionary);
            valuesDictionary.SetValue("StartingPositionMode", StartingPositionMode);
        }

        valuesDictionary.SetValue("BlockTextureName", BlocksTextureName);
        valuesDictionary.SetValue("Palette", Palette.Save());
        valuesDictionary.SetValue("MaxOnlinePlayerCount", MaxOnlinePlayerCount);
        valuesDictionary.SetValue("DisableBlocks", DisableBlocks);
        valuesDictionary.SetValue("RandomSpawnPosition", RandomSpawnPosition);
    }
}
