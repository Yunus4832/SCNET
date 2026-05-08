using System.Globalization;
using System.Xml.Linq;

namespace Game.Screens;

public class WorldOptionsScreen : Screen
{
    private const string _typeName = "WorldOptionsScreen";

    private static readonly float[] _yearDays = [8f, 12f, 16f, 20f, 24f, 32f, 48f, 64f, 96f];

    private static readonly float[] _islandSizes =
    [
        30f, 40f, 50f, 60f, 80f,
        100f, 120f, 150f, 200f, 250f,
        300f, 400f, 500f, 600f, 800f,
        1000f, 1200f, 1500f, 2000f, 2500f
    ];

    private static readonly float[] _biomeSizes =
    [
        0.25f, 0.33f, 0.5f, 0.75f,
        1f, 1.5f, 2f, 3f, 4f
    ];

    private readonly CheckboxWidget _areSeasonsChangingCheckBox;

    private readonly Widget _seasonsPanel;

    private readonly SliderWidget _timeOfYearSlider;

    private readonly Widget _yearDaysPanel;

    private readonly SliderWidget _yearDaysSlider;

    private readonly ButtonWidget _adventureRespawnButton;

    private readonly ButtonWidget _adventureSurvivalMechanicsButton;

    private readonly SliderWidget _biomeSizeSlider;

    private readonly ButtonWidget _blocksTextureButton;

    private readonly LabelWidget _blocksTextureDetails;

    private readonly RectangleWidget _blocksTextureIcon;

    private readonly LabelWidget _blocksTextureLabel;

    private readonly BlocksTexturesCache _blockTexturesCache = new();

    private readonly Widget _continentTerrainPanel;

    private readonly Widget _creativeModePanel;

    private readonly LabelWidget _descriptionLabel;

    private readonly ButtonWidget _environmentBehaviorButton;

    private readonly BlockIconWidget _flatTerrainBlock;

    private readonly ButtonWidget _flatTerrainBlockButton;

    private readonly LabelWidget _flatTerrainBlockLabel;

    private readonly SliderWidget _flatTerrainLevelSlider;

    private readonly CheckboxWidget _flatTerrainMagmaOceanCheckbox;

    private readonly Widget _flatTerrainPanel;

    private readonly SliderWidget _flatTerrainShoreRoughnessSlider;

    private readonly ButtonWidget _friendlyFireButton;

    private readonly SliderWidget _humidityOffsetSlider;

    private bool _isExistingWorld;

    private readonly SliderWidget _islandSizeEw;

    private readonly SliderWidget _islandSizeNs;

    private readonly Widget _islandTerrainPanel;
    private readonly Widget _newWorldOnlyPanel;

    private readonly ButtonWidget _paletteButton;

    private readonly SliderWidget _seaLevelOffsetSlider;

    private readonly ButtonWidget _supernaturalCreaturesButton;

    private readonly SliderWidget _temperatureOffsetSlider;

    private readonly ButtonWidget _terrainGenerationButton;

    private readonly ButtonWidget _timeOfDayButton;

    private readonly ButtonWidget _weatherEffectsButton;

    private WorldSettings _worldSettings = null!;

    public WorldOptionsScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/WorldOptionsScreen");
        LoadContents(this, node);
        _creativeModePanel = Children.Find<Widget>("CreativeModePanel")!;
        _seasonsPanel = Children.Find<Widget>("SeasonsPanel")!;
        _yearDaysPanel = Children.Find<Widget>("YearDaysPanel")!;
        _newWorldOnlyPanel = Children.Find<Widget>("NewWorldOnlyPanel")!;
        _continentTerrainPanel = Children.Find<Widget>("ContinentTerrainPanel")!;
        _islandTerrainPanel = Children.Find<Widget>("IslandTerrainPanel")!;
        _islandSizeNs = Children.Find<SliderWidget>("IslandSizeNS")!;
        _islandSizeEw = Children.Find<SliderWidget>("IslandSizeEW")!;
        _flatTerrainPanel = Children.Find<Widget>("FlatTerrainPanel")!;
        _blocksTextureIcon = Children.Find<RectangleWidget>("BlocksTextureIcon")!;
        _blocksTextureLabel = Children.Find<LabelWidget>("BlocksTextureLabel")!;
        _blocksTextureDetails = Children.Find<LabelWidget>("BlocksTextureDetails")!;
        _blocksTextureButton = Children.Find<ButtonWidget>("BlocksTextureButton")!;
        _seaLevelOffsetSlider = Children.Find<SliderWidget>("SeaLevelOffset")!;
        _temperatureOffsetSlider = Children.Find<SliderWidget>("TemperatureOffset")!;
        _humidityOffsetSlider = Children.Find<SliderWidget>("HumidityOffset")!;
        _biomeSizeSlider = Children.Find<SliderWidget>("BiomeSize")!;
        _paletteButton = Children.Find<ButtonWidget>("Palette")!;
        _supernaturalCreaturesButton = Children.Find<ButtonWidget>("SupernaturalCreatures")!;
        _friendlyFireButton = Children.Find<ButtonWidget>("FriendlyFire")!;
        _environmentBehaviorButton = Children.Find<ButtonWidget>("EnvironmentBehavior")!;
        _timeOfDayButton = Children.Find<ButtonWidget>("TimeOfDay")!;
        _areSeasonsChangingCheckBox = Children.Find<CheckboxWidget>("AreSeasonsChanging")!;
        _yearDaysSlider = Children.Find<SliderWidget>("YearDays")!;
        _timeOfYearSlider = Children.Find<SliderWidget>("TimeOfYear")!;
        _weatherEffectsButton = Children.Find<ButtonWidget>("WeatherEffects")!;
        _adventureRespawnButton = Children.Find<ButtonWidget>("AdventureRespawn")!;
        _adventureSurvivalMechanicsButton = Children.Find<ButtonWidget>("AdventureSurvivalMechanics")!;
        _terrainGenerationButton = Children.Find<ButtonWidget>("TerrainGeneration")!;
        _flatTerrainLevelSlider = Children.Find<SliderWidget>("FlatTerrainLevel")!;
        _flatTerrainShoreRoughnessSlider = Children.Find<SliderWidget>("FlatTerrainShoreRoughness")!;
        _flatTerrainBlock = Children.Find<BlockIconWidget>("FlatTerrainBlock")!;
        _flatTerrainBlockLabel = Children.Find<LabelWidget>("FlatTerrainBlockLabel")!;
        _flatTerrainBlockButton = Children.Find<ButtonWidget>("FlatTerrainBlockButton")!;
        _flatTerrainMagmaOceanCheckbox = Children.Find<CheckboxWidget>("MagmaOcean")!;
        _descriptionLabel = Children.Find<LabelWidget>("Description")!;
        _islandSizeEw.MinValue = 0f;
        _islandSizeEw.MaxValue = _islandSizes.Length - 1;
        _islandSizeEw.Granularity = 1f;
        _islandSizeNs.MinValue = 0f;
        _islandSizeNs.MaxValue = _islandSizes.Length - 1;
        _islandSizeNs.Granularity = 1f;
        _biomeSizeSlider.MinValue = 0f;
        _biomeSizeSlider.MaxValue = _biomeSizes.Length - 1;
        _biomeSizeSlider.Granularity = 1f;
        _yearDaysSlider.MinValue = 0f;
        _yearDaysSlider.MaxValue = _yearDays.Length - 1;
        _yearDaysSlider.Granularity = 1f;
    }

    public static string FormatOffset(float value)
    {
        if (value != 0f)
        {
            return (value >= 0f ? "+" : "") + value;
        }

        return LanguageControl.Get(_typeName, 6);
    }

    public override void Enter(object[] parameters)
    {
        _worldSettings = (WorldSettings)parameters[0];
        _isExistingWorld = (bool)parameters[1];
        _descriptionLabel.Text =
            StringsManager.GetString("EnvironmentBehaviorMode." + _worldSettings.EnvironmentBehaviorMode +
                                     ".Description");
    }

    public override void Leave()
    {
        _blockTexturesCache.Clear();
    }

    public override void Update()
    {
        if (_terrainGenerationButton.IsClicked && !_isExistingWorld)
        {
            var enumValues = EnumUtils.GetEnumValues(typeof(TerrainGenerationMode));
            DialogsManager.ShowDialog(null, new ListSelectionDialog(LanguageControl.Get(_typeName, 1), enumValues, 56f,
                e => StringsManager.GetString("TerrainGenerationMode." + (TerrainGenerationMode)e + ".Name"),
                delegate(object e)
                {
                    if (_worldSettings.GameMode != 0 &&
                        ((TerrainGenerationMode)e == TerrainGenerationMode.FlatContinent ||
                         (TerrainGenerationMode)e == TerrainGenerationMode.FlatIsland))
                    {
                        DialogsManager.ShowDialog(
                            null,
                            new MessageDialog(
                                LanguageControl.Get(_typeName, 4),
                                LanguageControl.Get(_typeName, 5),
                                LanguageControl.Get("Usual", "ok")
                            )
                        );
                    }
                    else
                    {
                        _worldSettings.TerrainGenerationMode = (TerrainGenerationMode)e;
                        _descriptionLabel.Text = StringsManager.GetString("TerrainGenerationMode." +
                                                                          _worldSettings.TerrainGenerationMode +
                                                                          ".Description");
                    }
                }));
        }

        if (_islandSizeEw.IsSliding && !_isExistingWorld)
        {
            _worldSettings.IslandSize.X =
                _islandSizes[MathUtils.Clamp((int)_islandSizeEw.Value, 0, _islandSizes.Length - 1)];
        }

        if (_islandSizeNs.IsSliding && !_isExistingWorld)
        {
            _worldSettings.IslandSize.Y =
                _islandSizes[MathUtils.Clamp((int)_islandSizeNs.Value, 0, _islandSizes.Length - 1)];
        }

        if (_flatTerrainLevelSlider.IsSliding && !_isExistingWorld)
        {
            _worldSettings.TerrainLevel =
                MathUtils.Clamp(
                    (int)_flatTerrainLevelSlider.Value / (int)_flatTerrainLevelSlider.Granularity *
                    (int)_flatTerrainLevelSlider.Granularity, 2, 252);
            _descriptionLabel.Text = StringsManager.GetString("FlatTerrainLevel.Description");
        }

        if (_flatTerrainShoreRoughnessSlider.IsSliding && !_isExistingWorld)
        {
            _worldSettings.ShoreRoughness = _flatTerrainShoreRoughnessSlider.Value;
            _descriptionLabel.Text = StringsManager.GetString("FlatTerrainShoreRoughness.Description");
        }

        if (_flatTerrainBlockButton.IsClicked && !_isExistingWorld)
        {
            var items = new[]
            {
                8, 2, 7, 3, 67,
                66, 4, 5, 26, 73,
                21, 46, 47, 15, 62,
                68, 126, 71, 1
            };
            DialogsManager.ShowDialog(
                null,
                new ListSelectionDialog(
                    LanguageControl.Get(_typeName, 2),
                    items,
                    72f,
                    delegate(object index)
                    {
                        var node2 = ContentManager.Get<XElement>("Widgets/SelectBlockItem");
                        var obj2 = (ContainerWidget)LoadWidget(null, node2, null);
                        obj2.Children.Find<BlockIconWidget>("SelectBlockItem.Block")!.Contents = (int)index;
                        obj2.Children.Find<LabelWidget>("SelectBlockItem.Text")!.Text = BlocksManager.Blocks[(int)index]
                            .GetDisplayName(null, Terrain.MakeBlockValue((int)index));
                        return obj2;
                    },
                    delegate(object index) { _worldSettings.TerrainBlockIndex = (int)index; }
                )
            );
        }

        if (_flatTerrainMagmaOceanCheckbox.IsClicked)
        {
            _worldSettings.TerrainOceanBlockIndex = _worldSettings.TerrainOceanBlockIndex == 18 ? 92 : 18;
            _descriptionLabel.Text = StringsManager.GetString("FlatTerrainMagmaOcean.Description");
        }

        if (_seaLevelOffsetSlider.IsSliding && !_isExistingWorld)
        {
            _worldSettings.SeaLevelOffset = (int)_seaLevelOffsetSlider.Value;
            _descriptionLabel.Text = StringsManager.GetString("SeaLevelOffset.Description");
        }

        if (_temperatureOffsetSlider.IsSliding && !_isExistingWorld)
        {
            _worldSettings.TemperatureOffset = _temperatureOffsetSlider.Value;
            _descriptionLabel.Text = StringsManager.GetString("TemperatureOffset.Description");
        }

        if (_humidityOffsetSlider.IsSliding && !_isExistingWorld)
        {
            _worldSettings.HumidityOffset = _humidityOffsetSlider.Value;
            _descriptionLabel.Text = StringsManager.GetString("HumidityOffset.Description");
        }

        if (_biomeSizeSlider.IsSliding && !_isExistingWorld)
        {
            _worldSettings.BiomeSize =
                _biomeSizes[MathUtils.Clamp((int)_biomeSizeSlider.Value, 0, _biomeSizes.Length - 1)];
            _descriptionLabel.Text = StringsManager.GetString("BiomeSize.Description");
        }

        if (_blocksTextureButton.IsClicked)
        {
            BlocksTexturesManager.UpdateBlocksTexturesList();
            var dialog = new ListSelectionDialog(LanguageControl.Get(_typeName, 3),
                BlocksTexturesManager.ReadOnlyBlockTexturesNames, 64f, delegate(object item)
                {
                    var node = ContentManager.Get<XElement>("Widgets/BlocksTextureItem");
                    var obj = (ContainerWidget)LoadWidget(this, node, null);
                    var texture2 = _blockTexturesCache.GetTexture((string)item);
                    obj.Children.Find<LabelWidget>("BlocksTextureItem.Text")!.Text =
                        BlocksTexturesManager.GetDisplayName((string)item);
                    obj.Children.Find<LabelWidget>("BlocksTextureItem.Details")!.Text =
                        $"{texture2.Width}x{texture2.Height}";
                    obj.Children.Find<RectangleWidget>("BlocksTextureItem.Icon")!.Subtexture =
                        new Subtexture(texture2, Vector2.Zero, Vector2.One);
                    return obj;
                }, delegate(object item) { _worldSettings.BlocksTextureName = (string)item; });
            DialogsManager.ShowDialog(null, dialog);
            _descriptionLabel.Text = StringsManager.GetString("BlocksTexture.Description");
        }

        if (_paletteButton.IsClicked)
        {
            DialogsManager.ShowDialog(null, new EditPaletteDialog(_worldSettings.Palette));
        }

        if (_supernaturalCreaturesButton.IsClicked)
        {
            _worldSettings.AreSupernaturalCreaturesEnabled = !_worldSettings.AreSupernaturalCreaturesEnabled;
            _descriptionLabel.Text =
                StringsManager.GetString("SupernaturalCreatures." + _worldSettings.AreSupernaturalCreaturesEnabled);
        }

        if (_friendlyFireButton.IsClicked)
        {
            _worldSettings.IsFriendlyFireEnabled = !_worldSettings.IsFriendlyFireEnabled;
            _descriptionLabel.Text = StringsManager.GetString("FriendlyFire." + _worldSettings.IsFriendlyFireEnabled);
        }

        if (_environmentBehaviorButton.IsClicked)
        {
            var enumValues2 = EnumUtils.GetEnumValues(typeof(EnvironmentBehaviorMode));
            _worldSettings.EnvironmentBehaviorMode =
                (EnvironmentBehaviorMode)((enumValues2.IndexOf((int)_worldSettings.EnvironmentBehaviorMode) + 1) %
                                          enumValues2.Count);
            _descriptionLabel.Text = StringsManager.GetString("EnvironmentBehaviorMode." +
                                                              _worldSettings.EnvironmentBehaviorMode +
                                                              ".Description");
        }

        if (_timeOfDayButton.IsClicked)
        {
            DialogsManager.ShowDialog(null, new ListSelectionDialog(LanguageControl.Get(_typeName, "7"),
                EnumUtils.GetEnumValues(typeof(TimeOfDayMode)), 56f,
                e => LanguageControl.Get("TimeOfDayMode", ((TimeOfDayMode)e).ToString()), delegate(object e)
                {
                    _worldSettings.TimeOfDayMode = (TimeOfDayMode)e;
                    _descriptionLabel.Text =
                        StringsManager.GetString(string.Concat("TimeOfDayMode.", (TimeOfDayMode)e, ".Description"));
                }));
        }

        if (_areSeasonsChangingCheckBox.IsClicked)
        {
            _worldSettings.AreSeasonsChanging = !_worldSettings.AreSeasonsChanging;
            _descriptionLabel.Text =
                StringsManager.GetString($"AreSeasonsChanging.{_worldSettings.AreSeasonsChanging}");
        }

        if (_yearDaysSlider.IsSliding)
        {
            _worldSettings.YearDays =
                _yearDays[MathUtils.Clamp((int)_yearDaysSlider.Value, 0, _yearDays.Length - 1)];
            _descriptionLabel.Text = StringsManager.GetString("YearDays.Description");
        }

        if (_timeOfYearSlider.IsSliding)
        {
            _worldSettings.TimeOfYear = MathUtils.Clamp(_timeOfYearSlider.Value, 0f, 0.999f);
            _descriptionLabel.Text = StringsManager.GetString("TimeOfYear.Description");
        }

        if (_weatherEffectsButton.IsClicked)
        {
            _worldSettings.AreWeatherEffectsEnabled = !_worldSettings.AreWeatherEffectsEnabled;
            _descriptionLabel.Text =
                StringsManager.GetString("WeatherMode." + _worldSettings.AreWeatherEffectsEnabled);
        }

        if (_adventureRespawnButton.IsClicked)
        {
            _worldSettings.IsAdventureRespawnAllowed = !_worldSettings.IsAdventureRespawnAllowed;
            _descriptionLabel.Text =
                StringsManager.GetString("AdventureRespawnMode." + _worldSettings.IsAdventureRespawnAllowed);
        }

        if (_adventureSurvivalMechanicsButton.IsClicked)
        {
            _worldSettings.AreAdventureSurvivalMechanicsEnabled =
                !_worldSettings.AreAdventureSurvivalMechanicsEnabled;
            _descriptionLabel.Text = StringsManager.GetString("AdventureSurvivalMechanics." +
                                                              _worldSettings.AreAdventureSurvivalMechanicsEnabled);
        }

        _creativeModePanel.IsVisible = _worldSettings.GameMode == GameMode.Creative;
        _newWorldOnlyPanel.IsVisible = !_isExistingWorld;
        _seasonsPanel.IsVisible = _worldSettings.GameMode == GameMode.Creative || !_isExistingWorld;
        _continentTerrainPanel.IsVisible = _worldSettings.TerrainGenerationMode == TerrainGenerationMode.Continent ||
                                           _worldSettings.TerrainGenerationMode ==
                                           TerrainGenerationMode.FlatContinent;
        _islandTerrainPanel.IsVisible = _worldSettings.TerrainGenerationMode == TerrainGenerationMode.Island ||
                                        _worldSettings.TerrainGenerationMode == TerrainGenerationMode.FlatIsland;
        _flatTerrainPanel.IsVisible = _worldSettings.TerrainGenerationMode == TerrainGenerationMode.FlatContinent ||
                                      _worldSettings.TerrainGenerationMode == TerrainGenerationMode.FlatIsland;
        _yearDaysPanel.IsVisible = _worldSettings.AreSeasonsChanging;
        _terrainGenerationButton.Text =
            StringsManager.GetString("TerrainGenerationMode." + _worldSettings.TerrainGenerationMode + ".Name");
        _islandSizeEw.Value = FindNearestIndex(_islandSizes, _worldSettings.IslandSize.X);
        _islandSizeEw.Text = _worldSettings.IslandSize.X.ToString(CultureInfo.InvariantCulture);
        _islandSizeNs.Value = FindNearestIndex(_islandSizes, _worldSettings.IslandSize.Y);
        _islandSizeNs.Text = _worldSettings.IslandSize.Y.ToString(CultureInfo.InvariantCulture);
        _flatTerrainLevelSlider.Value = _worldSettings.TerrainLevel;
        _flatTerrainLevelSlider.Text = _worldSettings.TerrainLevel.ToString();
        _flatTerrainShoreRoughnessSlider.Value = _worldSettings.ShoreRoughness;
        _flatTerrainShoreRoughnessSlider.Text = $"{_worldSettings.ShoreRoughness * 100f:0}%";
        _flatTerrainBlock.Contents = _worldSettings.TerrainBlockIndex;
        _flatTerrainMagmaOceanCheckbox.IsChecked = _worldSettings.TerrainOceanBlockIndex == 92;
        var text = BlocksManager.Blocks[_worldSettings.TerrainBlockIndex]
            .GetDisplayName(null, Terrain.MakeBlockValue(_worldSettings.TerrainBlockIndex));
        _flatTerrainBlockLabel.Text = text.Length > 10 ? text[..10] + "..." : text;
        var texture = _blockTexturesCache.GetTexture(_worldSettings.BlocksTextureName);
        _blocksTextureIcon.Subtexture = new Subtexture(texture, Vector2.Zero, Vector2.One);
        _blocksTextureLabel.Text = BlocksTexturesManager.GetDisplayName(_worldSettings.BlocksTextureName);
        _blocksTextureDetails.Text = $"{texture.Width}x{texture.Height}";
        _seaLevelOffsetSlider.Value = _worldSettings.SeaLevelOffset;
        _seaLevelOffsetSlider.Text = FormatOffset(_worldSettings.SeaLevelOffset);
        _temperatureOffsetSlider.Value = _worldSettings.TemperatureOffset;
        _temperatureOffsetSlider.Text = FormatOffset(_worldSettings.TemperatureOffset);
        _humidityOffsetSlider.Value = _worldSettings.HumidityOffset;
        _humidityOffsetSlider.Text = FormatOffset(_worldSettings.HumidityOffset);
        _biomeSizeSlider.Value = FindNearestIndex(_biomeSizes, _worldSettings.BiomeSize);
        _biomeSizeSlider.Text = _worldSettings.BiomeSize + "x";
        _environmentBehaviorButton.Text = LanguageControl.Get("EnvironmentBehaviorMode",
            _worldSettings.EnvironmentBehaviorMode.ToString());
        _timeOfDayButton.Text = LanguageControl.Get("TimeOfDayMode", _worldSettings.TimeOfDayMode.ToString());
        _areSeasonsChangingCheckBox.IsChecked = _worldSettings.AreSeasonsChanging;
        _yearDaysSlider.Value = FindNearestIndex(_yearDays, _worldSettings.YearDays);
        _yearDaysSlider.Text = $"{_worldSettings.YearDays} days";
        _timeOfYearSlider.Value = _worldSettings.TimeOfYear;
        _timeOfYearSlider.Text = $"{SubsystemSeasons.GetTimeOfYearName(_worldSettings.TimeOfYear)}";
        _timeOfYearSlider.TextColor = SubsystemSeasons.GetTimeOfYearColor(_worldSettings.TimeOfYear);
        _weatherEffectsButton.Text = _worldSettings.AreWeatherEffectsEnabled
            ? LanguageControl.Get("Usual", "enable")
            : LanguageControl.Get("Usual", "disable");
        _adventureRespawnButton.Text = _worldSettings.IsAdventureRespawnAllowed
            ? LanguageControl.Get("Usual", "allowed")
            : LanguageControl.Get("Usual", "not allowed");
        _adventureSurvivalMechanicsButton.Text = _worldSettings.AreAdventureSurvivalMechanicsEnabled
            ? LanguageControl.Get("Usual", "enable")
            : LanguageControl.Get("Usual", "disable");
        _supernaturalCreaturesButton.Text = _worldSettings.AreSupernaturalCreaturesEnabled
            ? LanguageControl.Get("Usual", "enable")
            : LanguageControl.Get("Usual", "disable");
        _friendlyFireButton.Text = _worldSettings.IsFriendlyFireEnabled
            ? LanguageControl.Get("Usual", "allowed")
            : LanguageControl.Get("Usual", "not allowed");
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }

    private static int FindNearestIndex(float[] list, float v)
    {
        var num = 0;
        for (var i = 0; i < list.Length; i++)
        {
            if (MathUtils.Abs(list[i] - v) < MathUtils.Abs(list[num] - v))
            {
                num = i;
            }
        }

        return num;
    }
}
