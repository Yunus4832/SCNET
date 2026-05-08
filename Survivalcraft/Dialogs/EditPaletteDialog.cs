using System.Xml.Linq;
using Engine.Media;

namespace Game.Dialogs;

public class EditPaletteDialog : Dialog
{
    private readonly ButtonWidget _cancelButton;

    private readonly LinkWidget[] _labels = new LinkWidget[16];

    private readonly ContainerWidget _listPanel;

    private readonly ButtonWidget _okButton;

    private readonly WorldPalette _palette;

    private readonly BevelledButtonWidget[] _rectangles = new BevelledButtonWidget[16];

    private readonly ButtonWidget[] _resetButtons = new ButtonWidget[16];

    private readonly WorldPalette _tmpPalette;

    public EditPaletteDialog(WorldPalette palette)
    {
        var node = ContentManager.Get<XElement>("Dialogs/EditPaletteDialog");
        LoadContents(this, node);
        _listPanel = Children.Find<ContainerWidget>("EditPaletteDialog.ListPanel")!;
        _okButton = Children.Find<ButtonWidget>("EditPaletteDialog.OK")!;
        _cancelButton = Children.Find<ButtonWidget>("EditPaletteDialog.Cancel")!;
        for (var i = 0; i < 16; i++)
        {
            var obj = new StackPanelWidget
            {
                Direction = LayoutDirection.Horizontal,
                Children =
                {
                    new CanvasWidget
                    {
                        Size = new Vector2(32f, 60f),
                        Children =
                        {
                            new LabelWidget
                            {
                                Text = i + 1 + ".",
                                Color = Color.Gray,
                                HorizontalAlignment = WidgetAlignment.Far,
                                VerticalAlignment = WidgetAlignment.Center,
                                Font = ContentManager.Get<BitmapFont>("Fonts/Pericles")
                            }
                        }
                    },
                    new CanvasWidget
                    {
                        Size = new Vector2(10f, 0f)
                    }
                }
            };
            obj.Children.Add(_labels[i] = new LinkWidget
            {
                Size = new Vector2(300f, -1f),
                VerticalAlignment = WidgetAlignment.Center,
                Font = ContentManager.Get<BitmapFont>("Fonts/Pericles")
            });
            obj.Children.Add(new CanvasWidget
            {
                Size = new Vector2(10f, 0f)
            });
            obj.Children.Add(_rectangles[i] = new BevelledButtonWidget
            {
                Size = new Vector2(1f / 0f, 60f),
                BevelSize = 1f,
                AmbientLight = 1f,
                DirectionalLight = 0.4f,
                VerticalAlignment = WidgetAlignment.Center
            });
            obj.Children.Add(new CanvasWidget
            {
                Size = new Vector2(10f, 0f)
            });
            obj.Children.Add(_resetButtons[i] = new BevelledButtonWidget
            {
                Size = new Vector2(160f, 60f),
                VerticalAlignment = WidgetAlignment.Center,
                Text = "Reset"
            });
            obj.Children.Add(new CanvasWidget
            {
                Size = new Vector2(10f, 0f)
            });
            var widget = obj;
            _listPanel.Children.Add(widget);
        }

        _palette = palette;
        _tmpPalette = new WorldPalette();
        _palette.CopyTo(_tmpPalette);
    }

    public override void Update()
    {
        for (var j = 0; j < 16; j++)
        {
            _labels[j].Text = _tmpPalette.Names[j];
            _rectangles[j].CenterColor = _tmpPalette.Colors[j];
            _resetButtons[j].IsEnabled = _tmpPalette.Colors[j] != WorldPalette.DefaultColors[j] ||
                                         _tmpPalette.Names[j] != LanguageControl.Get("WorldPalette", j);
        }

        for (var k = 0; k < 16; k++)
        {
            var i = k;
            if (_labels[k].IsClicked)
            {
                DialogsManager.ShowDialog(
                    this,
                    new TextBoxDialog(
                        "Edit Color Name",
                        _labels[k].Text,
                        16,
                        delegate(string s)
                        {
                            if (WorldPalette.VerifyColorName(s))
                            {
                                _tmpPalette.Names[i] = s;
                            }
                            else
                            {
                                DialogsManager.ShowDialog(
                                    this,
                                    new MessageDialog(
                                        "Invalid name",
                                        string.Empty,
                                        "OK"
                                    )
                                );
                            }
                        })
                );
            }

            if (_rectangles[k].IsClicked)
            {
                DialogsManager.ShowDialog(
                    this,
                    new EditColorDialog(_tmpPalette.Colors[k], delegate(Color? color)
                    {
                        if (color.HasValue)
                        {
                            _tmpPalette.Colors[i] = color.Value;
                        }
                    })
                );
            }

            if (!_resetButtons[k].IsClicked)
            {
                continue;
            }

            _tmpPalette.Colors[k] = WorldPalette.DefaultColors[k];
            _tmpPalette.Names[k] = LanguageControl.Get("WorldPalette", k);
        }

        if (_okButton.IsClicked)
        {
            _tmpPalette.CopyTo(_palette);
            Dismiss();
        }

        if (Input.Cancel || _cancelButton.IsClicked)
        {
            Dismiss();
        }
    }

    public void Dismiss()
    {
        DialogsManager.HideDialog(this);
    }
}
