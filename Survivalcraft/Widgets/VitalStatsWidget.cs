using System.Globalization;
using System.Xml.Linq;

using Game.Network.Transport;

namespace Game.Widgets;

public class VitalStatsWidget : CanvasWidget
{
    private const string _typeName = nameof(VitalStatsWidget);

    private readonly ButtonWidget _chokeButton;

    private readonly ComponentCharacter _componentCharacter;

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

    public VitalStatsWidget(ComponentCharacter componentCharacter)
    {
        _componentCharacter = componentCharacter;
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
            $"{_componentCharacter.CharacterData.Name}, Level {MathUtils.Floor(_componentCharacter.CharacterData.Level)} {_componentCharacter.CharacterData.CharacterSex.ToString()}";
        _healthValueBar.Value = _componentCharacter.ComponentHealth.Health;
        _staminaValueBar.Value = _componentCharacter.ComponentVitalStats.Stamina;
        _foodValueBar.Value = _componentCharacter.ComponentVitalStats.Food;
        _sleepValueBar.Value = _componentCharacter.ComponentVitalStats.Sleep;
        _temperatureValueBar.Value = _componentCharacter.ComponentVitalStats.Temperature / 24f;
        _wetnessValueBar.Value = _componentCharacter.ComponentVitalStats.Wetness;
        _experienceValueBar.Value =
            _componentCharacter.CharacterData.Level - MathUtils.Floor(_componentCharacter.CharacterData.Level);
        _strengthLabel.Text = string.Format(
            CultureInfo.InvariantCulture,
            "x {0:0.00}",
            _componentCharacter.ComponentLevel.StrengthFactor
        );
        _resilienceLabel.Text = string.Format(
            CultureInfo.InvariantCulture,
            "x {0:0.00}",
            _componentCharacter.ComponentLevel.ResilienceFactor
        );
        _speedLabel.Text = string.Format(
            CultureInfo.InvariantCulture,
            "x {0:0.00}",
            _componentCharacter.ComponentLevel.SpeedFactor
        );
        _hungerLabel.Text = string.Format(
            CultureInfo.InvariantCulture,
            "x {0:0.00}",
            _componentCharacter.ComponentLevel.HungerFactor
        );
        _insulationLabel.Text = string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.00} clo",
            _componentCharacter.ComponentClothing.Insulation
        );
        var helpScreen = ScreensManager.FindScreen<HelpScreen>("Help", true)!;
        if (_healthLink.IsClicked)
        {
            var topic = helpScreen.GetTopic("Health");
            DialogsManager.ShowDialog(
                _componentCharacter.GuiWidget,
                new MessageDialog(
                    topic.Title,
                    topic.Text,
                    LanguageManager.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    MessageDialog.CancelBehavior.InvokeButton1,
                    _ => { }
                )
            );
        }

        if (_staminaLink.IsClicked)
        {
            var topic2 = helpScreen.GetTopic("Stamina");
            DialogsManager.ShowDialog(
                _componentCharacter.GuiWidget,
                new MessageDialog(
                    topic2.Title,
                    topic2.Text,
                    LanguageManager.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    MessageDialog.CancelBehavior.InvokeButton1,
                    _ => { }
                )
            );
        }

        if (_foodLink.IsClicked)
        {
            var topic3 = helpScreen.GetTopic("Hunger");
            DialogsManager.ShowDialog(
                _componentCharacter.GuiWidget,
                new MessageDialog(
                    topic3.Title,
                    topic3.Text,
                    LanguageManager.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    MessageDialog.CancelBehavior.InvokeButton1,
                    _ => { }
                )
            );
        }

        if (_sleepLink.IsClicked)
        {
            var topic4 = helpScreen.GetTopic("Sleep");
            DialogsManager.ShowDialog(
                _componentCharacter.GuiWidget,
                new MessageDialog(
                    topic4.Title,
                    topic4.Text,
                    LanguageManager.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    MessageDialog.CancelBehavior.InvokeButton1,
                    _ => { }
                )
            );
        }

        if (_temperatureLink.IsClicked)
        {
            var topic5 = helpScreen.GetTopic("Temperature");
            DialogsManager.ShowDialog(
                _componentCharacter.GuiWidget,
                new MessageDialog(
                    topic5.Title,
                    topic5.Text,
                    LanguageManager.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    MessageDialog.CancelBehavior.InvokeButton1,
                    _ => { }
                )
            );
        }

        if (_wetnessLink.IsClicked)
        {
            var topic6 = helpScreen.GetTopic("Wetness");
            DialogsManager.ShowDialog(
                _componentCharacter.GuiWidget,
                new MessageDialog(
                    topic6.Title,
                    topic6.Text,
                    LanguageManager.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    MessageDialog.CancelBehavior.InvokeButton1,
                    _ => { }
                )
            );
        }

        if (_strengthLink.IsClicked)
        {
            var factors = new List<ComponentLevel.Factor>();
            var total = _componentCharacter.ComponentLevel.CalculateStrengthFactor(factors);
            DialogsManager.ShowDialog(
                _componentCharacter.GuiWidget,
                new LevelFactorDialog(
                    LanguageManager.GetContentWidgets(_typeName, "Strength"),
                    LanguageManager.GetContentWidgets(_typeName, 16),
                    factors,
                    total
                )
            );
        }

        if (_resilienceLink.IsClicked)
        {
            var factors2 = new List<ComponentLevel.Factor>();
            var total2 = _componentCharacter.ComponentLevel.CalculateResilienceFactor(factors2);
            DialogsManager.ShowDialog(
                _componentCharacter.GuiWidget,
                new LevelFactorDialog(
                    LanguageManager.GetContentWidgets(_typeName, "Resilience"),
                    LanguageManager.GetContentWidgets(_typeName, 17),
                    factors2,
                    total2
                )
            );
        }

        if (_speedLink.IsClicked)
        {
            var factors3 = new List<ComponentLevel.Factor>();
            var total3 = _componentCharacter.ComponentLevel.CalculateSpeedFactor(factors3);
            DialogsManager.ShowDialog(
                _componentCharacter.GuiWidget,
                new LevelFactorDialog(
                    LanguageManager.GetContentWidgets(_typeName, "Speed"),
                    LanguageManager.GetContentWidgets(_typeName, 18),
                    factors3,
                    total3
                )
            );
        }

        if (_hungerLink.IsClicked)
        {
            var factors4 = new List<ComponentLevel.Factor>();
            var total4 = _componentCharacter.ComponentLevel.CalculateHungerFactor(factors4);
            DialogsManager.ShowDialog(
                _componentCharacter.GuiWidget,
                new LevelFactorDialog(
                    LanguageManager.GetContentWidgets(_typeName, "Hunger"),
                    LanguageManager.GetContentWidgets(_typeName, 19),
                    factors4,
                    total4
                )
            );
        }

        if (_experienceLink.IsClicked)
        {
            var topic7 = helpScreen.GetTopic("Levels");
            DialogsManager.ShowDialog(
                _componentCharacter.GuiWidget,
                new MessageDialog(
                    topic7.Title,
                    topic7.Text,
                    LanguageManager.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    MessageDialog.CancelBehavior.InvokeButton1,
                    _ => { }
                )
            );
        }

        if (_insulationLink.IsClicked)
        {
            var topic8 = helpScreen.GetTopic("Clothing");
            DialogsManager.ShowDialog(
                _componentCharacter.GuiWidget,
                new MessageDialog(
                    topic8.Title,
                    topic8.Text,
                    LanguageManager.Get("Usual", "ok"),
                    string.Empty,
                    new Vector2(700f, 360f),
                    MessageDialog.CancelBehavior.InvokeButton1,
                    _ => { }
                )
            );
        }

        if (_chokeButton.IsClicked)
        {
            if (GameManager.Role == WorldRole.RemoteClient)
            {
                NetworkSender.SendToServer(
                    EntityStatePackageBuilder.Capture(
                        _componentCharacter.ComponentHealth,
                        null,
                        0.1f,
                        LanguageManager.GetContentWidgets(_typeName, "Choked"),
                        true,
                        true,
                        ComponentHealthPackage.RequestInjureType.Choke
                    )
                );
            }
            else
            {
                _componentCharacter.ComponentHealth.Injure(
                    0.1f,
                    null,
                    true,
                    LanguageManager.GetContentWidgets(_typeName, "Choked")
                );
            }
        }
    }
}
