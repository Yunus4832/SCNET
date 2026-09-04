using Engine.Media;
using Engine.Serialization;

namespace Game.Dialogs;

public class EditColorDialog : Dialog
{
    private readonly ButtonWidget _cancelButton;

    private Color _color;

    private readonly Action<Color?> _handler;

    private readonly LabelWidget _label;

    private readonly ButtonWidget _okButton;

    private readonly BevelledButtonWidget _rectangle;

    private readonly SliderWidget _sliderB;

    private readonly SliderWidget _sliderG;

    private readonly SliderWidget _sliderR;

    public EditColorDialog(Color color, Action<Color?> handler)
    {
        var obj = new CanvasWidget
        {
            Size = new Vector2(660f, 420f),
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center,
            Children =
            {
                new RectangleWidget
                {
                    FillColor = new Color(0, 0, 0, 255),
                    OutlineColor = new Color(128, 128, 128, 128),
                    OutlineThickness = 2f
                }
            }
        };
        var children2 = obj.Children;
        var obj2 = new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical,
            Margin = new Vector2(15f),
            HorizontalAlignment = WidgetAlignment.Center,
            Children =
            {
                new LabelWidget
                {
                    Text = "Edit Color",
                    HorizontalAlignment = WidgetAlignment.Center
                },
                new CanvasWidget
                {
                    Size = new Vector2(0f, 1f / 0f)
                }
            }
        };
        var children3 = obj2.Children;
        var obj3 = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal
        };
        var children4 = obj3.Children;
        var obj4 = new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical,
            VerticalAlignment = WidgetAlignment.Center
        };
        var children5 = obj4.Children;
        var obj5 = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal,
            HorizontalAlignment = WidgetAlignment.Far,
            Margin = new Vector2(0f, 10f),
            Children =
            {
                new LabelWidget
                {
                    Text = "Red:",
                    Color = Color.Gray,
                    VerticalAlignment = WidgetAlignment.Center,
                    Font = ContentManager.Get<BitmapFont>("Fonts/Pericles")
                },
                new CanvasWidget
                {
                    Size = new Vector2(10f, 0f)
                }
            }
        };
        var children6 = obj5.Children;
        var obj6 = new SliderWidget
        {
            Size = new Vector2(300f, 50f),
            IsLabelVisible = false,
            MinValue = 0f,
            MaxValue = 255f,
            Granularity = 1f,
            SoundName = ""
        };
        var widget = obj6;
        _sliderR = obj6;
        children6.Add(widget);
        children5.Add(obj5);
        var children7 = obj4.Children;
        var obj7 = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal,
            HorizontalAlignment = WidgetAlignment.Far,
            Margin = new Vector2(0f, 10f),
            Children =
            {
                new LabelWidget
                {
                    Text = "Green:",
                    Color = Color.Gray,
                    VerticalAlignment = WidgetAlignment.Center,
                    Font = ContentManager.Get<BitmapFont>("Fonts/Pericles")
                },
                new CanvasWidget
                {
                    Size = new Vector2(10f, 0f)
                }
            }
        };
        var children8 = obj7.Children;
        var obj8 = new SliderWidget
        {
            Size = new Vector2(300f, 50f),
            IsLabelVisible = false,
            MinValue = 0f,
            MaxValue = 255f,
            Granularity = 1f,
            SoundName = ""
        };
        widget = obj8;
        _sliderG = obj8;
        children8.Add(widget);
        children7.Add(obj7);
        var children9 = obj4.Children;
        var obj9 = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal,
            HorizontalAlignment = WidgetAlignment.Far,
            Margin = new Vector2(0f, 10f),
            Children =
            {
                new LabelWidget
                {
                    Text = "Blue:",
                    Color = Color.Gray,
                    VerticalAlignment = WidgetAlignment.Center,
                    Font = ContentManager.Get<BitmapFont>("Fonts/Pericles")
                },
                new CanvasWidget
                {
                    Size = new Vector2(10f, 0f)
                }
            }
        };
        var children10 = obj9.Children;
        var obj10 = new SliderWidget
        {
            Size = new Vector2(300f, 50f),
            IsLabelVisible = false,
            MinValue = 0f,
            MaxValue = 255f,
            Granularity = 1f,
            SoundName = ""
        };
        widget = obj10;
        _sliderB = obj10;
        children10.Add(widget);
        children9.Add(obj9);
        children4.Add(obj4);
        obj3.Children.Add(new CanvasWidget
        {
            Size = new Vector2(20f, 0f)
        });
        var children11 = obj3.Children;
        var canvasWidget = new CanvasWidget();
        var children12 = canvasWidget.Children;
        var obj11 = new BevelledButtonWidget
        {
            Size = new Vector2(200f, 240f),
            AmbientLight = 1f,
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center
        };
        _rectangle = obj11;
        children12.Add(obj11);
        var children13 = canvasWidget.Children;
        var obj12 = new LabelWidget
        {
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center,
            Font = ContentManager.Get<BitmapFont>("Fonts/Pericles")
        };
        _label = obj12;
        children13.Add(obj12);
        children11.Add(canvasWidget);
        children3.Add(obj3);
        obj2.Children.Add(new CanvasWidget
        {
            Size = new Vector2(0f, 1f / 0f)
        });
        var children14 = obj2.Children;
        var obj13 = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal,
            HorizontalAlignment = WidgetAlignment.Center
        };
        var children15 = obj13.Children;
        var obj14 = new BevelledButtonWidget
        {
            Size = new Vector2(160f, 60f),
            Text = LanguageManager.Ok
        };
        ButtonWidget widget4 = obj14;
        _okButton = obj14;
        children15.Add(widget4);
        obj13.Children.Add(new CanvasWidget
        {
            Size = new Vector2(50f, 0f)
        });
        var children16 = obj13.Children;
        var obj15 = new BevelledButtonWidget
        {
            Size = new Vector2(160f, 60f),
            Text = LanguageManager.Cancel
        };
        widget4 = obj15;
        _cancelButton = obj15;
        children16.Add(widget4);
        children14.Add(obj13);
        children2.Add(obj2);
        Children.Add(obj);
        _handler = handler;
        _color = color;
        UpdateControls();
    }

    public override void Update()
    {
        if (_rectangle.IsClicked)
        {
            DialogsManager.ShowDialog(
                this,
                new TextBoxDialog(
                    "Enter Color",
                    GetColorString(),
                    20,
                    delegate (string s)
                    {
                        try
                        {
                            _color.RGB = HumanReadableConverter.ConvertFromString<Color>(s);
                        }
                        catch
                        {
                            DialogsManager.ShowDialog(
                                this,
                                new MessageDialog(
                                    "Invalid Color",
                                    "Use R,G,B or #HEX notation, e.g. 255,92,13 or #FF5C0D",
                                    LanguageManager.Ok
                                )
                            );
                        }
                    }
                )
            );
        }

        if (_sliderR.IsSliding)
        {
            _color.R = (byte)_sliderR.Value;
        }

        if (_sliderG.IsSliding)
        {
            _color.G = (byte)_sliderG.Value;
        }

        if (_sliderB.IsSliding)
        {
            _color.B = (byte)_sliderB.Value;
        }

        if (_okButton.IsClicked)
        {
            Dismiss(_color);
        }

        if (Input.Cancel || _cancelButton.IsClicked)
        {
            Dismiss(null);
        }

        UpdateControls();
    }

    public void UpdateControls()
    {
        _rectangle.CenterColor = _color;
        _sliderR.Value = _color.R;
        _sliderG.Value = _color.G;
        _sliderB.Value = _color.B;
        _label.Text = GetColorString();
    }

    public string GetColorString()
    {
        return $"#{_color.R:X2}{_color.G:X2}{_color.B:X2}";
    }

    public void Dismiss(Color? result)
    {
        DialogsManager.HideDialog(this);
        _handler.Invoke(result);
    }
}
