using System.Globalization;
using System.Xml.Linq;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Widgets;

public class VitalStatsWidget : CanvasWidget
{
    private const string _typeName = "VitalStatsWidget";

    private readonly ButtonWidget _chokeButton;

    private readonly ComponentPlayer _componentPlayer;

    private readonly LinkWidget _experienceLink;

    private readonly ValueBarWidget _experienceValueBar;

    private readonly LinkWidget _foodLink;

    private readonly ValueBarWidget _foodValueBar;

    private readonly LinkWidget _healthLink;

    private readonly ValueBarWidget _healthValueBar;

    private readonly LabelWidget _hungerLabel;

    private readonly LinkWidget _hungerLink;

    private readonly LabelWidget _insulationLabel;

    private readonly LinkWidget _insulationLink;

    private readonly LabelWidget _resilienceLabel;

    private readonly LinkWidget _resilienceLink;

    private readonly LinkWidget _sleepLink;

    private readonly ValueBarWidget _sleepValueBar;

    private readonly LabelWidget _speedLabel;

    private readonly LinkWidget _speedLink;

    private readonly LinkWidget _staminaLink;

    private readonly ValueBarWidget _staminaValueBar;

    private readonly LabelWidget _strengthLabel;

    private readonly LinkWidget _strengthLink;

    private readonly LinkWidget _temperatureLink;

    private readonly ValueBarWidget _temperatureValueBar;

    private readonly LabelWidget _titleLabel;

    private readonly LinkWidget _wetnessLink;

    private readonly ValueBarWidget _wetnessValueBar;

    public VitalStatsWidget(ComponentPlayer componentPlayer)
    {
        _componentPlayer = componentPlayer;
        var node = ContentManager.Get<XElement>("Widgets/VitalStatsWidget");
        LoadContents(this, node);
        _titleLabel = Children.Find<LabelWidget>("TitleLabel")!;
        _healthLink = Children.Find<LinkWidget>("HealthLink")!;
        _healthValueBar = Children.Find<ValueBarWidget>("HealthValueBar")!;
        _staminaLink = Children.Find<LinkWidget>("StaminaLink")!;
        _staminaValueBar = Children.Find<ValueBarWidget>("StaminaValueBar")!;
        _foodLink = Children.Find<LinkWidget>("FoodLink")!;
        _foodValueBar = Children.Find<ValueBarWidget>("FoodValueBar")!;
        _sleepLink = Children.Find<LinkWidget>("SleepLink")!;
        _sleepValueBar = Children.Find<ValueBarWidget>("SleepValueBar")!;
        _temperatureLink = Children.Find<LinkWidget>("TemperatureLink")!;
        _temperatureValueBar = Children.Find<ValueBarWidget>("TemperatureValueBar")!;
        _wetnessLink = Children.Find<LinkWidget>("WetnessLink")!;
        _wetnessValueBar = Children.Find<ValueBarWidget>("WetnessValueBar")!;
        _chokeButton = Children.Find<ButtonWidget>("ChokeButton")!;
        _strengthLink = Children.Find<LinkWidget>("StrengthLink")!;
        _strengthLabel = Children.Find<LabelWidget>("StrengthLabel")!;
        _resilienceLink = Children.Find<LinkWidget>("ResilienceLink")!;
        _resilienceLabel = Children.Find<LabelWidget>("ResilienceLabel")!;
        _speedLink = Children.Find<LinkWidget>("SpeedLink")!;
        _speedLabel = Children.Find<LabelWidget>("SpeedLabel")!;
        _hungerLink = Children.Find<LinkWidget>("HungerLink")!;
        _hungerLabel = Children.Find<LabelWidget>("HungerLabel")!;
        _experienceLink = Children.Find<LinkWidget>("ExperienceLink")!;
        _experienceValueBar = Children.Find<ValueBarWidget>("ExperienceValueBar")!;
        _insulationLink = Children.Find<LinkWidget>("InsulationLink")!;
        _insulationLabel = Children.Find<LabelWidget>("InsulationLabel")!;
    }

    public override void Update()
    {
        _titleLabel.Text =
            $"{_componentPlayer.PlayerData.Name}, Level {MathUtils.Floor(_componentPlayer.PlayerData.Level)} {_componentPlayer.PlayerData.PlayerClass.ToString()}";
        _healthValueBar.Value = _componentPlayer.ComponentHealth.Health;
        _staminaValueBar.Value = _componentPlayer.ComponentVitalStats.Stamina;
        _foodValueBar.Value = _componentPlayer.ComponentVitalStats.Food;
        _sleepValueBar.Value = _componentPlayer.ComponentVitalStats.Sleep;
        _temperatureValueBar.Value = _componentPlayer.ComponentVitalStats.Temperature / 24f;
        _wetnessValueBar.Value = _componentPlayer.ComponentVitalStats.Wetness;
        _experienceValueBar.Value =
            _componentPlayer.PlayerData.Level - MathUtils.Floor(_componentPlayer.PlayerData.Level);
        _strengthLabel.Text = string.Format(CultureInfo.InvariantCulture, "x {0:0.00}",
            _componentPlayer.ComponentLevel.StrengthFactor);
        _resilienceLabel.Text = string.Format(CultureInfo.InvariantCulture, "x {0:0.00}",
            _componentPlayer.ComponentLevel.ResilienceFactor);
        _speedLabel.Text = string.Format(CultureInfo.InvariantCulture, "x {0:0.00}",
            _componentPlayer.ComponentLevel.SpeedFactor);
        _hungerLabel.Text = string.Format(CultureInfo.InvariantCulture, "x {0:0.00}",
            _componentPlayer.ComponentLevel.HungerFactor);
        _insulationLabel.Text = string.Format(CultureInfo.InvariantCulture, "{0:0.00} clo",
            _componentPlayer.ComponentClothing.Insulation);
        var helpScreen = ScreensManager.FindScreen<HelpScreen>("Help", true)!;
        if (_healthLink.IsClicked)
        {
            var topic = helpScreen.GetTopic("Health");
            DialogsManager.ShowDialog(_componentPlayer.GuiWidget,
                new MessageDialog(
                    topic.Title,
                    topic.Text,
                    LanguageControl.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    _ => { }
                )
            );
        }

        if (_staminaLink.IsClicked)
        {
            var topic2 = helpScreen.GetTopic("Stamina");
            DialogsManager.ShowDialog(_componentPlayer.GuiWidget,
                new MessageDialog(
                    topic2.Title,
                    topic2.Text,
                    LanguageControl.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    _ => { }
                )
            );
        }

        if (_foodLink.IsClicked)
        {
            var topic3 = helpScreen.GetTopic("Hunger");
            DialogsManager.ShowDialog(_componentPlayer.GuiWidget,
                new MessageDialog(
                    topic3.Title,
                    topic3.Text,
                    LanguageControl.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    _ => { }
                )
            );
        }

        if (_sleepLink.IsClicked)
        {
            var topic4 = helpScreen.GetTopic("Sleep");
            DialogsManager.ShowDialog(_componentPlayer.GuiWidget,
                new MessageDialog(
                    topic4.Title,
                    topic4.Text,
                    LanguageControl.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f), _ => { }
                )
            );
        }

        if (_temperatureLink.IsClicked)
        {
            var topic5 = helpScreen.GetTopic("Temperature");
            DialogsManager.ShowDialog(_componentPlayer.GuiWidget,
                new MessageDialog(
                    topic5.Title,
                    topic5.Text,
                    LanguageControl.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    _ => { }
                )
            );
        }

        if (_wetnessLink.IsClicked)
        {
            var topic6 = helpScreen.GetTopic("Wetness");
            DialogsManager.ShowDialog(_componentPlayer.GuiWidget,
                new MessageDialog(
                    topic6.Title,
                    topic6.Text,
                    LanguageControl.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    _ => { }
                )
            );
        }

        if (_strengthLink.IsClicked)
        {
            var factors = new List<ComponentLevel.Factor>();
            var total = _componentPlayer.ComponentLevel.CalculateStrengthFactor(factors);
            DialogsManager.ShowDialog(_componentPlayer.GuiWidget,
                new LevelFactorDialog(LanguageControl.GetContentWidgets(_typeName, "Strength"),
                    LanguageControl.GetContentWidgets(_typeName, 16), factors, total));
        }

        if (_resilienceLink.IsClicked)
        {
            var factors2 = new List<ComponentLevel.Factor>();
            var total2 = _componentPlayer.ComponentLevel.CalculateResilienceFactor(factors2);
            DialogsManager.ShowDialog(_componentPlayer.GuiWidget,
                new LevelFactorDialog(LanguageControl.GetContentWidgets(_typeName, "Resilience"),
                    LanguageControl.GetContentWidgets(_typeName, 17), factors2, total2));
        }

        if (_speedLink.IsClicked)
        {
            var factors3 = new List<ComponentLevel.Factor>();
            var total3 = _componentPlayer.ComponentLevel.CalculateSpeedFactor(factors3);
            DialogsManager.ShowDialog(_componentPlayer.GuiWidget,
                new LevelFactorDialog(LanguageControl.GetContentWidgets(_typeName, "Speed"),
                    LanguageControl.GetContentWidgets(_typeName, 18), factors3, total3));
        }

        if (_hungerLink.IsClicked)
        {
            var factors4 = new List<ComponentLevel.Factor>();
            var total4 = _componentPlayer.ComponentLevel.CalculateHungerFactor(factors4);
            DialogsManager.ShowDialog(_componentPlayer.GuiWidget,
                new LevelFactorDialog(LanguageControl.GetContentWidgets(_typeName, "Hunger"),
                    LanguageControl.GetContentWidgets(_typeName, 19), factors4, total4));
        }

        if (_experienceLink.IsClicked)
        {
            var topic7 = helpScreen.GetTopic("Levels");
            DialogsManager.ShowDialog(_componentPlayer.GuiWidget,
                new MessageDialog(
                    topic7.Title,
                    topic7.Text,
                    LanguageControl.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    _ => { }
                )
            );
        }

        if (_insulationLink.IsClicked)
        {
            var topic8 = helpScreen.GetTopic("Clothing");
            DialogsManager.ShowDialog(_componentPlayer.GuiWidget,
                new MessageDialog(
                    topic8.Title,
                    topic8.Text,
                    LanguageControl.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    _ => { }));
        }

        if (_chokeButton.IsClicked)
        {
            if (CommonLib.WorkType == WorkType.Client)
            {
                CommonLib.Net.QueuePackage(new ComponentHealthPackage(_componentPlayer.ComponentHealth, null, 0.1f,
                    LanguageControl.GetContentWidgets(_typeName, "Choked"), true, true,
                    ComponentHealthPackage.RequestInjureType.Choke));
            }
            else
            {
                _componentPlayer.ComponentHealth.Injure(0.1f, null, true,
                    LanguageControl.GetContentWidgets(_typeName, "Choked"));
            }
        }
    }
}
