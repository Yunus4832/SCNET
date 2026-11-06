using System.Xml;

using EntitySystem.TemplatesDatabase;

namespace Game.World;

public class WorldSettings
{
    public const ushort MaximumOnlinePlayerCount = 32;

    public bool AreAdventureSurvivalMechanicsEnabled = true;

    public bool AreSeasonsChanging = true;

    public bool AreSupernaturalCreaturesEnabled = true;

    public bool AreWeatherEffectsEnabled = true;

    public float BiomeSize = 1f;

    public string BlocksTextureName = string.Empty;

    public float CreativeDigTime = 0.2f;

    public float CreativeReach = 7.5f;

    public float DaySpeed = 1f;

    public string DisableBlocks = string.Empty;

    public EnvironmentBehaviorMode EnvironmentBehaviorMode;

    public GameMode GameMode = GameMode.Survival;

    public float HumidityOffset;

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

    public Vector2 IslandSize = new(400f, 400f);

    public string KeywordBlocking = string.Empty;

    public ushort MaxOnlinePlayerCount { get; set => field = Math.Min(value, MaximumOnlinePlayerCount); } = 20;

    public string Name = string.Empty;

    public WorldPalette Palette = new();

    public bool RandomSpawnPosition;

    public float RecoverFactor = 1f;

    public int SeaLevelOffset;

    public string Seed = string.Empty;

    public float ShoreRoughness = 0.5f;

    public StartingPositionMode StartingPositionMode;

    public float TemperatureOffset;

    public int TerrainBlockIndex = 8;

    public TerrainGenerationMode TerrainGenerationMode;

    public int TerrainLevel = 64;

    public int TerrainOceanBlockIndex = 18;

    public TimeOfDayMode TimeOfDayMode;

    public float TimeOfYear = SeasonCalendar.MidSummer;

    public float YearDays = 24f;

    public void ResetOptionsForNonCreativeMode()
    {
        if (TerrainGenerationModes.IsFlat(TerrainGenerationMode))
        {
            TerrainGenerationMode = TerrainGenerationModes.ToNonFlatMode(TerrainGenerationMode);
        }

        EnvironmentBehaviorMode = EnvironmentBehaviorMode.Living;
        TimeOfDayMode = TimeOfDayMode.Changing;
        AreWeatherEffectsEnabled = true;
        IsAdventureRespawnAllowed = true;
        AreAdventureSurvivalMechanicsEnabled = true;
        TerrainLevel = 64;
        ShoreRoughness = 0.5f;
        TerrainBlockIndex = 8;
    }

    public void ResetOptionsForNonCreativeMode(WorldSettings? originalWorldSettings)
    {
        if (TerrainGenerationModes.IsFlat(TerrainGenerationMode))
        {
            TerrainGenerationMode = TerrainGenerationModes.ToNonFlatMode(TerrainGenerationMode);
        }

        EnvironmentBehaviorMode = EnvironmentBehaviorMode.Living;
        TimeOfDayMode = TimeOfDayMode.Changing;
        AreWeatherEffectsEnabled = true;
        IsAdventureRespawnAllowed = true;
        AreAdventureSurvivalMechanicsEnabled = true;
        TerrainLevel = 64;
        ShoreRoughness = 0.5f;
        TerrainBlockIndex = 8;
        if (originalWorldSettings == null)
        {
            return;
        }

        AreSeasonsChanging = originalWorldSettings.AreSeasonsChanging;
        YearDays = originalWorldSettings.YearDays;
        TimeOfYear = originalWorldSettings.TimeOfYear;
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
        TerrainGenerationMode = valuesDictionary.GetValue("TerrainGenerationMode", TerrainGenerationMode.Continent);
        IslandSize = valuesDictionary.GetValue("IslandSize", new Vector2(200f, 200f));
        TerrainLevel = valuesDictionary.GetValue("TerrainLevel", 64);
        ShoreRoughness = valuesDictionary.GetValue("ShoreRoughness", 0f);
        KeywordBlocking = valuesDictionary.GetValue("KeywordBlocking", string.Empty);
        KeywordBlocking = XmlConvert.DecodeName(KeywordBlocking);
        DaySpeed = valuesDictionary.GetValue("DaySpeed", 1f);
        RecoverFactor = valuesDictionary.GetValue("RecoverFactor", 1f);
        TerrainBlockIndex = valuesDictionary.GetValue("TerrainBlockIndex", 8);
        TerrainOceanBlockIndex = valuesDictionary.GetValue("TerrainOceanBlockIndex", 18);
        TemperatureOffset = valuesDictionary.GetValue("TemperatureOffset", 0f);
        HumidityOffset = valuesDictionary.GetValue("HumidityOffset", 0f);
        SeaLevelOffset = valuesDictionary.GetValue("SeaLevelOffset", 0);
        BiomeSize = valuesDictionary.GetValue("BiomeSize", 1f);
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
            valuesDictionary.SetValue("TerrainGenerationMode", TerrainGenerationMode);
            valuesDictionary.SetValue("IslandSize", IslandSize);
            valuesDictionary.SetValue("TerrainLevel", TerrainLevel);
            valuesDictionary.SetValue("ShoreRoughness", ShoreRoughness);
            valuesDictionary.SetValue("TerrainBlockIndex", TerrainBlockIndex);
            valuesDictionary.SetValue("TerrainOceanBlockIndex", TerrainOceanBlockIndex);
            valuesDictionary.SetValue("TemperatureOffset", TemperatureOffset);
            valuesDictionary.SetValue("HumidityOffset", HumidityOffset);
            valuesDictionary.SetValue("SeaLevelOffset", SeaLevelOffset);
            valuesDictionary.SetValue("BiomeSize", BiomeSize);
            valuesDictionary.SetValue("StartingPositionMode", StartingPositionMode);
        }

        valuesDictionary.SetValue("BlockTextureName", BlocksTextureName);
        valuesDictionary.SetValue("Palette", Palette.Save());
        valuesDictionary.SetValue("MaxOnlinePlayerCount", MaxOnlinePlayerCount);
        valuesDictionary.SetValue("DisableBlocks", DisableBlocks);
        valuesDictionary.SetValue("RandomSpawnPosition", RandomSpawnPosition);
    }
}
