using System.Globalization;
using System.Xml.Linq;

using Engine.Graphics;

using Game.Application;
using Game.Blocks.Presentation.Geometry;
using Game.Terrains.Generation;
using Game.World.Presentation;

namespace Game.Screens;

public class WorldOptionsScreen : Screen
{
    private const string _typeName = nameof(WorldOptionsScreen);

    private static readonly float[] _yearDays = [8f, 12f, 16f, 20f, 24f, 32f, 48f, 64f, 96f];

    private readonly StackPanelWidget _generatorParameters;

    private readonly List<Action> _updateGeneratorRows = [];

    private readonly CheckboxWidget _areSeasonsChangingCheckBox;

    private readonly Widget _seasonsPanel;

    private readonly SliderWidget _timeOfYearSlider;

    private readonly Widget _yearDaysPanel;

    private readonly SliderWidget _yearDaysSlider;

    private readonly ButtonWidget _adventureRespawnButton;

    private readonly ButtonWidget _adventureSurvivalMechanicsButton;

    private readonly ClickableWidget _blocksTextureSelector;

    private readonly LabelWidget _blocksTextureDetails;

    private readonly RectangleWidget _blocksTextureIcon;

    private readonly LabelWidget _blocksTextureLabel;

    private readonly BlocksTexturesCache _blockTexturesCache = new();

    private readonly Widget _creativeModePanel;

    private readonly LabelWidget _descriptionLabel;

    private readonly (ClickableTextRowWidget Label, Func<string> DescriptionProvider)[] _descriptionBindings;

    private readonly ButtonWidget _environmentBehaviorButton;

    private readonly ButtonWidget _friendlyFireButton;

    private readonly SliderWidget _hotbarSlotsCountSlider;

    private bool _isExistingWorld;

    private readonly ButtonWidget _paletteButton;

    private readonly ButtonWidget _supernaturalCreaturesButton;

    private readonly InlineSelectionWidget _terrainGenerationSelection;

    private bool _updatingTerrainGeneration;

    private readonly InlineSelectionWidget _timeOfDaySelection;

    private bool _updatingTimeOfDay;

    private readonly ButtonWidget _weatherEffectsButton;

    private readonly SliderWidget _creativeDigTimeSlider;

    private readonly SliderWidget _creativeReachSlider;

    private WorldSettings _worldSettings = null!;

    public WorldOptionsScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/WorldOptionsScreen");
        LoadContents(this, node);
        _creativeModePanel = Children.Find<Widget>("CreativeModePanel")!;
        _creativeDigTimeSlider = Children.Find<SliderWidget>("CreativeDigTimeSlider")!;
        _creativeReachSlider = Children.Find<SliderWidget>("CreativeReachSlider")!;
        _seasonsPanel = Children.Find<Widget>("SeasonsPanel")!;
        _yearDaysPanel = Children.Find<Widget>("YearDaysPanel")!;
        _blocksTextureIcon = Children.Find<RectangleWidget>("BlocksTextureIcon")!;
        _blocksTextureLabel = Children.Find<LabelWidget>("BlocksTextureLabel")!;
        _blocksTextureDetails = Children.Find<LabelWidget>("BlocksTextureDetails")!;
        _blocksTextureSelector = Children.Find<ClickableWidget>("BlocksTextureSelector")!;
        _hotbarSlotsCountSlider = Children.Find<SliderWidget>("HotbarSlotsCount")!;
        _paletteButton = Children.Find<ButtonWidget>("Palette")!;
        _supernaturalCreaturesButton = Children.Find<ButtonWidget>("SupernaturalCreatures")!;
        _friendlyFireButton = Children.Find<ButtonWidget>("FriendlyFire")!;
        _environmentBehaviorButton = Children.Find<ButtonWidget>("EnvironmentBehavior")!;
        _timeOfDaySelection = Children.Find<InlineSelectionWidget>("TimeOfDay")!;
        _timeOfDaySelection.ItemTextProvider = item =>
            Localization.ClientLanguage.Catalog.Get("TimeOfDayMode", ((TimeOfDayMode)item).ToString());
        _timeOfDaySelection.SelectionChanged += TimeOfDayChanged;
        _areSeasonsChangingCheckBox = Children.Find<CheckboxWidget>("AreSeasonsChanging")!;
        _yearDaysSlider = Children.Find<SliderWidget>("YearDays")!;
        _timeOfYearSlider = Children.Find<SliderWidget>("TimeOfYear")!;
        _weatherEffectsButton = Children.Find<ButtonWidget>("WeatherEffects")!;
        _adventureRespawnButton = Children.Find<ButtonWidget>("AdventureRespawn")!;
        _adventureSurvivalMechanicsButton = Children.Find<ButtonWidget>("AdventureSurvivalMechanics")!;
        _terrainGenerationSelection = Children.Find<InlineSelectionWidget>("TerrainGeneration")!;
        _generatorParameters = Children.Find<StackPanelWidget>("GeneratorParameters")!;
        _terrainGenerationSelection.ItemTextProvider = item =>
            Localization.ClientLanguage.Catalog.Get("TerrainGenerators", ((TerrainGeneratorDefinition)item).NameKey);
        _terrainGenerationSelection.SelectionChanged += TerrainGenerationChanged;
        _descriptionLabel = Children.Find<LabelWidget>("Description")!;
        _descriptionBindings =
        [
            CreateDescriptionBinding(
                "TerrainGenerationLabel",
                () => Localization.ClientLanguage.Catalog.Get(
                    "TerrainGenerators",
                    _worldSettings.Generator.Definition.DescriptionKey
                )
            ),
            CreateDescriptionBinding(
                "BlocksTextureSettingLabel",
                () => StringsManager.GetString("BlocksTexture", "Description")
            ),
            CreateDescriptionBinding("PaletteLabel", () => StringsManager.GetString("Palette", "Description")),
            CreateDescriptionBinding(
                "HotbarSlotsCountLabel",
                () => Localization.ClientLanguage.Catalog.GetContentWidgets(
                    _typeName,
                    "HotbarSlotsCountDescription"
                )
            ),
            CreateDescriptionBinding(
                "AreSeasonsChangingLabel",
                () => StringsManager.GetString("AreSeasonsChanging", _worldSettings.AreSeasonsChanging)
            ),
            CreateDescriptionBinding("YearDaysLabel", () => StringsManager.GetString("YearDays", "Description")),
            CreateDescriptionBinding("TimeOfYearLabel", () => StringsManager.GetString("TimeOfYear", "Description")),
            CreateDescriptionBinding(
                "EnvironmentBehaviorLabel",
                () => StringsManager.GetString(
                    "EnvironmentBehaviorMode",
                    _worldSettings.EnvironmentBehaviorMode,
                    "Description"
                )
            ),
            CreateDescriptionBinding(
                "TimeOfDayLabel",
                () => StringsManager.GetString("TimeOfDayMode", _worldSettings.TimeOfDayMode, "Description")
            ),
            CreateDescriptionBinding(
                "WeatherEffectsLabel",
                () => StringsManager.GetString("WeatherMode", _worldSettings.AreWeatherEffectsEnabled)
            ),
            CreateDescriptionBinding(
                "AdventureRespawnLabel",
                () => StringsManager.GetString("AdventureRespawnMode", _worldSettings.IsAdventureRespawnAllowed)
            ),
            CreateDescriptionBinding(
                "AdventureSurvivalMechanicsLabel",
                () => StringsManager.GetString(
                    "AdventureSurvivalMechanics",
                    _worldSettings.AreAdventureSurvivalMechanicsEnabled
                )
            ),
            CreateDescriptionBinding(
                "SupernaturalCreaturesLabel",
                () => StringsManager.GetString("SupernaturalCreatures", _worldSettings.AreSupernaturalCreaturesEnabled)
            ),
            CreateDescriptionBinding(
                "FriendlyFireLabel",
                () => StringsManager.GetString("FriendlyFire", _worldSettings.IsFriendlyFireEnabled)
            )
        ];
        _yearDaysSlider.MinValue = 0f;
        _yearDaysSlider.MaxValue = _yearDays.Length - 1;
        _yearDaysSlider.Granularity = 1f;
    }

    private (ClickableTextRowWidget Label, Func<string> DescriptionProvider) CreateDescriptionBinding(
        string labelName,
        Func<string> descriptionProvider
    )
    {
        return (Children.Find<ClickableTextRowWidget>(labelName)!, descriptionProvider);
    }

    private void UpdateSelectedDescription()
    {
        foreach (var (label, descriptionProvider) in _descriptionBindings)
        {
            if (label.IsClicked)
            {
                _descriptionLabel.Text = descriptionProvider();
                break;
            }
        }
    }

    private void RefreshTerrainGenerationSelection()
    {
        _updatingTerrainGeneration = true;
        _terrainGenerationSelection.SetItems(
            TerrainGeneratorDefinition.All.Where(definition =>
                _isExistingWorld || _worldSettings.GameMode == GameMode.Creative || !definition.CreativeOnly
            )
        );
        _terrainGenerationSelection.SelectedItem = _worldSettings.Generator.Definition;
        _terrainGenerationSelection.IsEnabled = !_isExistingWorld;
        _updatingTerrainGeneration = false;
        BuildGeneratorRows();
    }

    private void TerrainGenerationChanged()
    {
        if (_updatingTerrainGeneration ||
            _isExistingWorld ||
            _terrainGenerationSelection.SelectedItem is not TerrainGeneratorDefinition definition) { return; }

        _worldSettings.Generator = new TerrainGeneratorSettings(definition);
        BuildGeneratorRows();
    }

    private void BuildGeneratorRows()
    {
        _generatorParameters.Children.Clear();
        _updateGeneratorRows.Clear();
        var settings = _worldSettings.Generator;
        foreach (var parameter in settings.Definition.Parameters)
        {
            var row = new UniformSpacingPanelWidget
                { Direction = LayoutDirection.Horizontal, Margin = new Vector2(0, 3) };
            var label = new ClickableTextRowWidget
            {
                Text = Localization.ClientLanguage.Catalog.Get("TerrainGeneratorParameters", parameter.NameKey),
                HorizontalAlignment = WidgetAlignment.Far, VerticalAlignment = WidgetAlignment.Center,
                TextHorizontalAlignment = WidgetAlignment.Far, Margin = new Vector2(20, 0)
            };
            row.Children.Add(label);
            _updateGeneratorRows.Add(() =>
                {
                    if (label.IsClicked)
                    {
                        _descriptionLabel.Text = Localization.ClientLanguage.Catalog.Get(
                            "TerrainGeneratorParameters",
                            parameter.DescriptionKey
                        );
                    }
                }
            );
            var editor = CreateGeneratorEditor(settings, parameter);
            editor.Name = "GeneratorParameter." + parameter.Key;
            editor.IsEnabled = !_isExistingWorld;
            editor.Margin = new Vector2(20, 0);
            if (editor is InlineSelectionWidget selection)
            {
                label.Size = new Vector2(float.PositiveInfinity, selection.CollapsedSize.Y);
                label.VerticalAlignment = WidgetAlignment.Near;
                selection.VerticalAlignment = WidgetAlignment.Near;
            }
            else
            {
                editor.VerticalAlignment = WidgetAlignment.Center;
            }

            row.Children.Add(editor);
            _generatorParameters.Children.Add(row);
        }
    }

    private Widget CreateGeneratorEditor(TerrainGeneratorSettings settings, TerrainGeneratorParameter parameter)
    {
        if (parameter.Kind == TerrainParameterKind.Number)
        {
            var choices = parameter.SuggestedValues;
            var value = settings.Get<double>(parameter.Key);
            // Session overrides can specify an intermediate value between the UI presets.
            var stops = choices.Count > 0 ? choices.Append(value).Distinct().Order().ToArray() : [];
            var slider = new SliderWidget
            {
                Size = new Vector2(410, 60),
                MinValue = stops.Length > 0 ? 0 : (float)parameter.Minimum,
                MaxValue = stops.Length > 0 ? stops.Length - 1 : (float)parameter.Maximum,
                Granularity = stops.Length > 0 ? 1 : (float)parameter.Step,
                Value = stops.Length > 0 ? Array.IndexOf(stops, value) : (float)value,
                Text = TerrainParameterText.Format(parameter, value)
            };
            _updateGeneratorRows.Add(() =>
                {
                    if (_isExistingWorld || !slider.IsSliding) { return; }

                    var current = stops.Length > 0
                        ? stops[(int)slider.Value]
                        : parameter.Minimum +
                          Math.Round((slider.Value - parameter.Minimum) / parameter.Step) * parameter.Step;
                    current = Math.Clamp(current, parameter.Minimum, parameter.Maximum);
                    settings.Set(parameter.Key, current);
                    slider.Text = TerrainParameterText.Format(parameter, current);
                }
            );
            return slider;
        }

        if (parameter.Kind == TerrainParameterKind.Boolean)
        {
            var checkbox = new CheckboxWidget
                { IsChecked = settings.Get<bool>(parameter.Key), IsAutoCheckingEnabled = true };
            _updateGeneratorRows.Add(() =>
                {
                    if (!_isExistingWorld && checkbox.IsClicked) { settings.Set(parameter.Key, checkbox.IsChecked); }
                }
            );
            return checkbox;
        }

        if (parameter.Options.Count > 0)
        {
            var selection = new InlineSelectionWidget { CollapsedSize = new Vector2(310, 60) };
            selection.ItemTextProvider = item => TerrainParameterText.Format(parameter, item);
            var options = parameter.Options.Where(option => !parameter.UsesBlockNames ||
                                                            ClientApplication.Content.PreviewCatalog.Rules.Registry
                                                                .TryGet(ResourceId.Parse(option), out _)
            );
            selection.SetItems(options);
            selection.SelectedItem = settings.Get<string>(parameter.Key);
            selection.SelectionChanged += () =>
            {
                if (!_isExistingWorld && selection.SelectedItem is string selected)
                {
                    settings.Set(parameter.Key, selected);
                }
            };
            return selection;
        }

        var box = new TextBoxWidget { Text = settings.Get<string>(parameter.Key), Size = new Vector2(310, 60) };
        box.TextChanged += editor =>
        {
            if (!_isExistingWorld) { settings.Set(parameter.Key, editor.Text); }
        };
        return box;
    }

    private void RefreshTimeOfDaySelection()
    {
        _updatingTimeOfDay = true;
        _timeOfDaySelection.SetItems(Enum.GetValues<TimeOfDayMode>().Cast<object>());
        _timeOfDaySelection.SelectedItem = _worldSettings.TimeOfDayMode;
        _updatingTimeOfDay = false;
    }

    private void TimeOfDayChanged()
    {
        if (!_updatingTimeOfDay && _timeOfDaySelection.SelectedItem is TimeOfDayMode mode)
        {
            _worldSettings.TimeOfDayMode = mode;
        }
    }


    public override void Enter(object[] parameters)
    {
        _worldSettings = (WorldSettings)parameters[0];
        _isExistingWorld = (bool)parameters[1];
        RefreshTerrainGenerationSelection();
        RefreshTimeOfDaySelection();
        _descriptionLabel.Text =
            Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "DefaultDescription");
    }

    public override void Leave()
    {
        _terrainGenerationSelection.Close();
        _timeOfDaySelection.Close();
        _blockTexturesCache.Clear();
    }

    public override void Update()
    {
        UpdateSelectedDescription();

        if (_creativeDigTimeSlider.IsSliding)
        {
            _worldSettings.CreativeDigTime = _creativeDigTimeSlider.Value;
        }

        if (_creativeReachSlider.IsSliding)
        {
            _worldSettings.CreativeReach = _creativeReachSlider.Value;
        }

        foreach (var updateRow in _updateGeneratorRows) { updateRow(); }

        if (_blocksTextureSelector.IsClicked)
        {
            BlocksTexturesManager.UpdateBlocksTexturesList();
            var dialog = new ListSelectionDialog(
                Localization.ClientLanguage.Catalog.Get(_typeName, 3),
                BlocksTexturesManager.ReadOnlyBlockTexturesNames,
                64f,
                delegate(object item)
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
                },
                delegate(object item) { _worldSettings.BlocksTextureName = (string)item; },
                _worldSettings.BlocksTextureName
            );
            DialogsManager.ShowDialog(null, dialog);
        }

        if (_paletteButton.IsClicked)
        {
            DialogsManager.ShowDialog(null, new EditPaletteDialog(_worldSettings.Palette));
        }

        if (_hotbarSlotsCountSlider.IsSliding)
        {
            _worldSettings.CurrentHotbarSlotsCount = (int)_hotbarSlotsCountSlider.Value;
        }

        if (_supernaturalCreaturesButton.IsClicked)
        {
            _worldSettings.AreSupernaturalCreaturesEnabled = !_worldSettings.AreSupernaturalCreaturesEnabled;
        }

        if (_friendlyFireButton.IsClicked)
        {
            _worldSettings.IsFriendlyFireEnabled = !_worldSettings.IsFriendlyFireEnabled;
        }

        if (_environmentBehaviorButton.IsClicked)
        {
            var enumValues2 = EnumUtils.GetEnumValues(typeof(EnvironmentBehaviorMode));
            _worldSettings.EnvironmentBehaviorMode =
                (EnvironmentBehaviorMode)((enumValues2.IndexOf((int)_worldSettings.EnvironmentBehaviorMode) + 1) %
                                          enumValues2.Count);
        }

        if (_areSeasonsChangingCheckBox.IsClicked)
        {
            _worldSettings.AreSeasonsChanging = !_worldSettings.AreSeasonsChanging;
        }

        if (_yearDaysSlider.IsSliding)
        {
            _worldSettings.YearDays =
                _yearDays[MathUtils.Clamp((int)_yearDaysSlider.Value, 0, _yearDays.Length - 1)];
        }

        if (_timeOfYearSlider.IsSliding)
        {
            _worldSettings.TimeOfYear = MathUtils.Clamp(_timeOfYearSlider.Value, 0f, 0.999f);
        }

        if (_weatherEffectsButton.IsClicked)
        {
            _worldSettings.AreWeatherEffectsEnabled = !_worldSettings.AreWeatherEffectsEnabled;
        }

        if (_adventureRespawnButton.IsClicked)
        {
            _worldSettings.IsAdventureRespawnAllowed = !_worldSettings.IsAdventureRespawnAllowed;
        }

        if (_adventureSurvivalMechanicsButton.IsClicked)
        {
            _worldSettings.AreAdventureSurvivalMechanicsEnabled =
                !_worldSettings.AreAdventureSurvivalMechanicsEnabled;
        }

        _creativeModePanel.IsVisible = _worldSettings.GameMode == GameMode.Creative;
        _seasonsPanel.IsVisible = _worldSettings.GameMode == GameMode.Creative || !_isExistingWorld;
        _yearDaysPanel.IsVisible = _worldSettings.AreSeasonsChanging;
        var texture = _blockTexturesCache.GetTexture(_worldSettings.BlocksTextureName);
        _blocksTextureIcon.Subtexture = new Subtexture(texture, Vector2.Zero, Vector2.One);
        _blocksTextureLabel.Text = BlocksTexturesManager.GetDisplayName(_worldSettings.BlocksTextureName);
        _blocksTextureDetails.Text = $"{texture.Width}x{texture.Height}";
        _creativeDigTimeSlider.Value = _worldSettings.CreativeDigTime;
        _creativeDigTimeSlider.Text = $"{MathUtils.Round(1000f * _worldSettings.CreativeDigTime)}ms";
        _creativeReachSlider.Value = _worldSettings.CreativeReach;
        _creativeReachSlider.Text = string.Format(
            Localization.ClientLanguage.Catalog.Get("SettingsControlsScreen", 1),
            $"{_worldSettings.CreativeReach:0.0} "
        );
        _hotbarSlotsCountSlider.Value = _worldSettings.CurrentHotbarSlotsCount;
        _hotbarSlotsCountSlider.Text = _worldSettings.CurrentHotbarSlotsCount.ToString();
        _environmentBehaviorButton.Text = Localization.ClientLanguage.Catalog.Get(
            "EnvironmentBehaviorMode",
            _worldSettings.EnvironmentBehaviorMode.ToString()
        );
        _areSeasonsChangingCheckBox.IsChecked = _worldSettings.AreSeasonsChanging;
        _yearDaysSlider.Value = FindNearestIndex(_yearDays, _worldSettings.YearDays);
        _yearDaysSlider.Text = string.Format(
            Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, "DaysCount"),
            _worldSettings.YearDays
        );
        _timeOfYearSlider.Value = _worldSettings.TimeOfYear;
        _timeOfYearSlider.Text = $"{ClientApplication.Content.Seasons.GetTimeOfYearName(_worldSettings.TimeOfYear)}";
        _timeOfYearSlider.TextColor = ClientApplication.Content.Seasons.GetTimeOfYearColor(_worldSettings.TimeOfYear);
        _weatherEffectsButton.Text = _worldSettings.AreWeatherEffectsEnabled
            ? Localization.ClientLanguage.Catalog.Get("Usual", "enable")
            : Localization.ClientLanguage.Catalog.Get("Usual", "disable");
        _adventureRespawnButton.Text = _worldSettings.IsAdventureRespawnAllowed
            ? Localization.ClientLanguage.Catalog.Get("Usual", "allowed")
            : Localization.ClientLanguage.Catalog.Get("Usual", "not allowed");
        _adventureSurvivalMechanicsButton.Text = _worldSettings.AreAdventureSurvivalMechanicsEnabled
            ? Localization.ClientLanguage.Catalog.Get("Usual", "enable")
            : Localization.ClientLanguage.Catalog.Get("Usual", "disable");
        _supernaturalCreaturesButton.Text = _worldSettings.AreSupernaturalCreaturesEnabled
            ? Localization.ClientLanguage.Catalog.Get("Usual", "enable")
            : Localization.ClientLanguage.Catalog.Get("Usual", "disable");
        _friendlyFireButton.Text = _worldSettings.IsFriendlyFireEnabled
            ? Localization.ClientLanguage.Catalog.Get("Usual", "allowed")
            : Localization.ClientLanguage.Catalog.Get("Usual", "not allowed");
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }

    private static int FindNearestIndex(float[] list, float v)
    {
        var nearestIndex = 0;
        for (var i = 0; i < list.Length; i++)
        {
            if (MathUtils.Abs(list[i] - v) < MathUtils.Abs(list[nearestIndex] - v))
            {
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }
}
