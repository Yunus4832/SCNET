using System.Xml.Linq;

using Engine.Media;

namespace Game.Screens;

public class BestiaryDescriptionScreen : Screen
{
    private const string _typeName = "BestiaryDescriptionScreen";

    private  readonly LabelWidget _descriptionWidget;

    private readonly ContainerWidget _dropsPanel;

    private int _index;

    private IList<BestiaryCreatureInfo> _infoList = [];

    private readonly ButtonWidget _leftButtonWidget;

    private readonly ModelWidget _modelWidget;

    private readonly LabelWidget _nameWidget;

    private readonly LabelWidget _propertyNames1Widget;

    private readonly LabelWidget _propertyNames2Widget;

    private readonly LabelWidget _propertyValues1Widget;

    private readonly LabelWidget _propertyValues2Widget;

    private readonly ButtonWidget _rightButtonWidget;

    public BestiaryDescriptionScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/BestiaryDescriptionScreen");
        LoadContents(this, node);
        _modelWidget = Children.Find<ModelWidget>("Model")!;
        _nameWidget = Children.Find<LabelWidget>("Name")!;
        _leftButtonWidget = Children.Find<ButtonWidget>("Left")!;
        _rightButtonWidget = Children.Find<ButtonWidget>("Right")!;
        _descriptionWidget = Children.Find<LabelWidget>("Description")!;
        _propertyNames1Widget = Children.Find<LabelWidget>("PropertyNames1")!;
        _propertyValues1Widget = Children.Find<LabelWidget>("PropertyValues1")!;
        _propertyNames2Widget = Children.Find<LabelWidget>("PropertyNames2")!;
        _propertyValues2Widget = Children.Find<LabelWidget>("PropertyValues2")!;
        _dropsPanel = Children.Find<ContainerWidget>("Drops")!;
    }

    public override void Enter(object[] parameters)
    {
        var item = (BestiaryCreatureInfo)parameters[0];
        _infoList = (IList<BestiaryCreatureInfo>)parameters[1];
        _index = _infoList.IndexOf(item);
        UpdateCreatureProperties();
    }

    public override void Update()
    {
        _leftButtonWidget.IsEnabled = _index > 0;
        _rightButtonWidget.IsEnabled = _index < _infoList.Count - 1;
        if (_leftButtonWidget.IsClicked || Input.Left)
        {
            _index = MathUtils.Max(_index - 1, 0);
            UpdateCreatureProperties();
        }

        if (_rightButtonWidget.IsClicked || Input.Right)
        {
            _index = MathUtils.Min(_index + 1, _infoList.Count - 1);
            UpdateCreatureProperties();
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }

    private void UpdateCreatureProperties()
    {
        if (_index < 0 || _index >= _infoList.Count)
        {
            return;
        }

        var bestiaryCreatureInfo = _infoList[_index];
        _modelWidget.AutoRotationVector = new Vector3(0f, 1f, 0f);
        BestiaryScreen.SetupBestiaryModelWidget(bestiaryCreatureInfo, _modelWidget, new Vector3(-1f, 0f, -1f),
            true, true);
        _nameWidget.Text = bestiaryCreatureInfo.DisplayName;
        _descriptionWidget.Text = bestiaryCreatureInfo.Description;
        _propertyNames1Widget.Text = string.Empty;
        _propertyValues1Widget.Text = string.Empty;
        _propertyNames1Widget.Text += LanguageControl.Get(_typeName, "resilience");
        _propertyValues1Widget.Text = _propertyValues1Widget.Text + bestiaryCreatureInfo.AttackResilience + "\n";
        _propertyNames1Widget.Text += LanguageControl.Get(_typeName, "attack");
        _propertyValues1Widget.Text = _propertyValues1Widget.Text + (bestiaryCreatureInfo.AttackPower > 0f
            ? bestiaryCreatureInfo.AttackPower.ToString("0.0")
            : LanguageControl.Get("Usual", "none")) + "\n";
        _propertyNames1Widget.Text += LanguageControl.Get(_typeName, "herding");
        _propertyValues1Widget.Text = _propertyValues1Widget.Text + (bestiaryCreatureInfo.IsHerding
            ? LanguageControl.Get("Usual", "yes")
            : LanguageControl.Get("Usual", "no")) + "\n";
        _propertyNames1Widget.Text += LanguageControl.Get(_typeName, 1);
        _propertyValues1Widget.Text = _propertyValues1Widget.Text + (bestiaryCreatureInfo.CanBeRidden
            ? LanguageControl.Get("Usual", "yes")
            : LanguageControl.Get("Usual", "no")) + "\n";
        _propertyNames1Widget.Text = _propertyNames1Widget.Text.TrimEnd();
        _propertyValues1Widget.Text = _propertyValues1Widget.Text.TrimEnd();
        _propertyNames2Widget.Text = string.Empty;
        _propertyValues2Widget.Text = string.Empty;
        _propertyNames2Widget.Text += LanguageControl.Get(_typeName, "speed");
        _propertyValues2Widget.Text = _propertyValues2Widget.Text +
                                      (bestiaryCreatureInfo.MovementSpeed * 3.6).ToString("0") +
                                      LanguageControl.Get(_typeName, "speed unit");
        _propertyNames2Widget.Text += LanguageControl.Get(_typeName, "jump height");
        _propertyValues2Widget.Text = _propertyValues2Widget.Text +
                                      bestiaryCreatureInfo.JumpHeight.ToString("0.0") +
                                      LanguageControl.Get(_typeName, "length unit");
        _propertyNames2Widget.Text += LanguageControl.Get(_typeName, "weight");
        _propertyValues2Widget.Text = _propertyValues2Widget.Text + bestiaryCreatureInfo.Mass +
                                      LanguageControl.Get(_typeName, "weight unit");
        _propertyNames2Widget.Text += LanguageControl.Get("BlocksManager", "Spawner Eggs");
        _propertyValues2Widget.Text = _propertyValues2Widget.Text + (bestiaryCreatureInfo.HasSpawnerEgg
            ? LanguageControl.Get("Usual", "yes")
            : LanguageControl.Get("Usual", "no")) + "\n";
        _propertyNames2Widget.Text = _propertyNames2Widget.Text.TrimEnd();
        _propertyValues2Widget.Text = _propertyValues2Widget.Text.TrimEnd();
        _dropsPanel.Children.Clear();
        if (bestiaryCreatureInfo.Loot.Count > 0)
        {
            foreach (var item in bestiaryCreatureInfo.Loot)
            {
                var text = item.MinCount >= item.MaxCount
                    ? $"{item.MinCount}"
                    : string.Format(LanguageControl.Get(_typeName, "range"), item.MinCount, item.MaxCount);
                if (item.Probability < 1f)
                {
                    text += string.Format(LanguageControl.Get(_typeName, 2), $"{item.Probability * 100f:0}");
                }

                _dropsPanel.Children.Add(new StackPanelWidget
                {
                    Margin = new Vector2(20f, 0f),
                    Children =
                    {
                        new BlockIconWidget
                        {
                            Size = new Vector2(32f),
                            Scale = 1.2f,
                            VerticalAlignment = WidgetAlignment.Center,
                            Value = item.Value
                        },
                        new CanvasWidget
                        {
                            Size = new Vector2(10f, 0f)
                        },
                        new LabelWidget
                        {
                            VerticalAlignment = WidgetAlignment.Center,
                            Text = text
                        }
                    }
                });
            }
        }
        else
        {
            _dropsPanel.Children.Add(new LabelWidget
            {
                Margin = new Vector2(20f, 0f),
                Font = ContentManager.Get<BitmapFont>("Fonts/Pericles"),
                Text = LanguageControl.Get("Usual", "nothing")
            });
        }
    }
}
