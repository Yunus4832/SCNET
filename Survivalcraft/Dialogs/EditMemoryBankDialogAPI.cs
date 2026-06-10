using System.Text;

namespace Game.Dialogs;

public class EditMemoryBankDialogApi : Dialog
{
    private int _clickPos;

    private readonly DynamicArray<byte> _data = [];

    private bool _isClick = true;

    private bool _isSetPos; //是否为设定位置模式

    public int LastValue;

    private readonly List<ClickTextWidget> _list = [];

    private StackPanelWidget _mainView;

    private readonly MemoryBankData _memory;

    private readonly Action _onCancel;

    private int _setPosN; //第几位数

    public EditMemoryBankDialogApi(MemoryBankData memoryBankData, Action onCancel)
    {
        _memory = memoryBankData;
        _data.Clear();
        _data.AddRange(_memory.Data);
        var canvasWidget = new CanvasWidget
        {
            Size = new Vector2(600f, 500f), HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center
        };
        var rectangleWidget = new RectangleWidget
            { FillColor = new Color(0, 0, 0, 255), OutlineColor = new Color(128, 128, 128, 128), OutlineThickness = 2 };
        var stackPanel = new StackPanelWidget { Direction = LayoutDirection.Vertical };
        var labelWidget = new LabelWidget
        {
            Text = LanguageManager.GetContentWidgets(GetType().Name, 0), HorizontalAlignment = WidgetAlignment.Center,
            Margin = new Vector2(0, 10)
        };
        var stackPanelWidget = new StackPanelWidget
        {
            Direction = LayoutDirection.Horizontal, HorizontalAlignment = WidgetAlignment.Near,
            VerticalAlignment = WidgetAlignment.Near, Margin = new Vector2(10f, 10f)
        };
        Children.Add(canvasWidget);
        canvasWidget.Children.Add(rectangleWidget);
        canvasWidget.Children.Add(stackPanel);
        stackPanel.Children.Add(labelWidget);
        stackPanel.Children.Add(stackPanelWidget);
        stackPanelWidget.Children.Add(InitData());
        stackPanelWidget.Children.Add(InitButton());
        _mainView = stackPanel;
        _onCancel = onCancel;
        LastValue = _memory.Read(0);
    }

    public byte LastOutput { get; set; }

    public byte Read(int address)
    {
        if (address >= 0 && address < _data.Count)
        {
            return _data.Array[address];
        }

        return 0;
    }

    public void Write(int address, byte data)
    {
        if (address >= 0 && address < _data.Count)
        {
            _data.Array[address] = data;
        }
        else if (address is >= 0 and < 256 && data != 0)
        {
            _data.Count = MathUtils.Max(_data.Count, address + 1);
            _data.Array[address] = data;
        }
    }

    public void LoadString(string data)
    {
        var array = data.Split([';'], StringSplitOptions.RemoveEmptyEntries);
        if (array.Length >= 1)
        {
            var text = array[0];
            text = text.TrimEnd('0');
            _data.Clear();
            for (var i = 0; i < MathUtils.Min(text.Length, 256); i++)
            {
                var num = MemoryBankData.HexChars.IndexOf(char.ToUpperInvariant(text[i]));
                if (num < 0)
                {
                    num = 0;
                }

                _data.Add((byte)num);
            }
        }

        if (array.Length < 2)
        {
            return;
        }

        var text2 = array[1];
        var num2 = MemoryBankData.HexChars.IndexOf(char.ToUpperInvariant(text2[0]));
        if (num2 < 0)
        {
            num2 = 0;
        }

        LastOutput = (byte)num2;
    }

    public string SaveString(bool saveLastOutput = true)
    {
        var stringBuilder = new StringBuilder();
        var num = _data.Count;
        for (var j = 0; j < num; j++)
        {
            var index = MathUtils.Clamp(_data.Array[j], 0, 15);
            stringBuilder.Append(MemoryBankData.HexChars[index]);
        }

        if (!saveLastOutput)
        {
            return stringBuilder.ToString();
        }

        stringBuilder.Append(';');
        stringBuilder.Append(MemoryBankData.HexChars[MathUtils.Clamp(LastOutput, 0, 15)]);

        return stringBuilder.ToString();
    }

    public Widget InitData()
    {
        var stack = new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical, VerticalAlignment = WidgetAlignment.Center,
            HorizontalAlignment = WidgetAlignment.Far, Margin = new Vector2(10, 0)
        };
        for (var i = 0; i < 17; i++)
        {
            var line = new StackPanelWidget { Direction = LayoutDirection.Horizontal };
            for (var j = 0; j < 17; j++)
            {
                var addr = (i - 1) * 16 + (j - 1);
                if (j > 0 && i > 0)
                {
                    var clickTextWidget = new ClickTextWidget(new Vector2(22),
                        $"{MemoryBankData.HexChars[Read(addr)]}", delegate
                        {
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                            _clickPos = addr;
                            _isClick = true;
                        });
                    _list.Add(clickTextWidget);
                    line.Children.Add(clickTextWidget);
                }
                else
                {
                    int p;
                    if (i == 0 && j > 0)
                    {
                        p = j - 1;
                    }
                    else if (j == 0 && i > 0)
                    {
                        p = i - 1;
                    }
                    else
                    {
                        var click = new ClickTextWidget(new Vector2(22), "", Actions.Empty);
                        line.Children.Add(click);
                        continue;
                    }

                    var clickTextWidget = new ClickTextWidget(new Vector2(22), MemoryBankData.HexChars[p].ToString(),
                        Actions.Empty);
                    clickTextWidget.LabelWidget.Color = Color.DarkGray;
                    line.Children.Add(clickTextWidget);
                }
            }

            stack.Children.Add(line);
        }

        return stack;
    }

    public Widget MakeFuncButton(string txt, Action func)
    {
        var clickText = new ClickTextWidget(new Vector2(40), txt, func, true);
        clickText.BorderColor = Color.White;
        clickText.Margin = new Vector2(2);
        clickText.LabelWidget.FontScale = txt.Length > 1 ? 0.7f : 1f;
        clickText.LabelWidget.Color = Color.White;
        return clickText;
    }

    public Widget InitButton()
    {
        var stack = new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical, VerticalAlignment = WidgetAlignment.Center,
            HorizontalAlignment = WidgetAlignment.Far, Margin = new Vector2(10, 10)
        };
        for (var i = 0; i < 6; i++)
        {
            var stackPanelWidget = new StackPanelWidget { Direction = LayoutDirection.Horizontal };
            for (var j = 0; j < 3; j++)
            {
                var cc = i * 3 + j;
                if (cc < 15)
                {
                    var pp = cc + 1;
                    stackPanelWidget.Children.Add(MakeFuncButton(string.Format("{0}", MemoryBankData.HexChars[pp]),
                        delegate
                        {
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                            if (!_isSetPos)
                            {
                                Write(_clickPos, (byte)pp); //写入数据
                                LastValue = pp;
                                _clickPos += 1; //自动加1
                                if (_clickPos >= 255)
                                {
                                    _clickPos = 0;
                                }

                                _isClick = true;
                            }
                            else
                            {
                                //处于设定位置模式
                                if (_setPosN == 0)
                                {
                                    _clickPos = 16 * pp;
                                }
                                else if (_setPosN == 1)
                                {
                                    _clickPos += pp;
                                }

                                _setPosN += 1;

                                if (_setPosN != 2)
                                {
                                    return;
                                }

                                if (_clickPos > 0xff)
                                {
                                    _clickPos = 0;
                                }

                                _setPosN = 0;
                                _isClick = true;
                                _isSetPos = false;
                            }
                        }));
                }
                else if (cc == 15)
                {
                    stackPanelWidget.Children.Add(MakeFuncButton(string.Format("{0}", MemoryBankData.HexChars[0]),
                        delegate
                        {
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                            if (!_isSetPos)
                            {
                                Write(_clickPos, 0); //写入数据
                                LastValue = 0;
                                _clickPos += 1; //自动加1
                                if (_clickPos >= 255)
                                {
                                    _clickPos = 0;
                                }

                                _isClick = true;
                            }
                            else
                            {
                                //处于设定位置模式
                                if (_setPosN == 0)
                                {
                                    _clickPos = 0;
                                }
                                else if (_setPosN == 1)
                                {
                                    _clickPos += 0;
                                }

                                _setPosN += 1;

                                if (_setPosN != 2)
                                {
                                    return;
                                }

                                if (_clickPos > 0xff)
                                {
                                    _clickPos = 0;
                                }

                                _setPosN = 0;
                                _isClick = true;
                                _isSetPos = false;
                            }
                        }));
                }
                else if (cc == 16)
                {
                    stackPanelWidget.Children.Add(MakeFuncButton(LanguageManager.GetContentWidgets(GetType().Name, 1),
                        delegate
                        {
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                            for (var ai = 0; ai < _data.Count; ai++)
                            {
                                Write(ai, 0);
                            }

                            _isClick = true;
                        }));
                }
                else if (cc == 17)
                {
                    stackPanelWidget.Children.Add(MakeFuncButton(LanguageManager.GetContentWidgets(GetType().Name, 2),
                        delegate
                        {
                            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
                            var tmp = new DynamicArray<byte>();
                            tmp.AddRange(_data);
                            tmp.Count = 256;
                            for (var c = 0; c < 16; c++)
                            for (var d = 0; d < 16; d++)
                            {
                                Write(c + d * 16, tmp[c * 16 + d]);
                            }

                            _clickPos = 0;
                            _isClick = true;
                        }));
                }
            }

            stack.Children.Add(stackPanelWidget);
        }

        var labelWidget = new LabelWidget
        {
            FontScale = 0.8f, Text = LanguageManager.GetContentWidgets(GetType().Name, 3),
            HorizontalAlignment = WidgetAlignment.Center, Margin = new Vector2(0f, 10f), Color = Color.DarkGray
        };
        stack.Children.Add(labelWidget);
        stack.Children.Add(MakeTextBox(delegate(TextBoxWidget textBoxWidget)
        {
            LoadString(textBoxWidget.Text);
            _isClick = true;
        }, _memory.SaveString(false)));
        stack.Children.Add(MakeButton(LanguageManager.GetContentWidgets(GetType().Name, 4), delegate
        {
            for (var i = 0; i < _data.Count; i++)
            {
                _memory.Write(i, _data[i]);
            }

            _onCancel.Invoke();
            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
            DialogsManager.HideDialog(this);
        }));
        stack.Children.Add(MakeButton(LanguageManager.GetContentWidgets(GetType().Name, 5), delegate
        {
            AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);
            DialogsManager.HideDialog(this);
            _isClick = true;
        }));
        return stack;
    }

    public Widget MakeTextBox(Action<TextBoxWidget> ac, string text = "")
    {
        var canvasWidget = new CanvasWidget { HorizontalAlignment = WidgetAlignment.Center };
        var rectangleWidget = new RectangleWidget
            { FillColor = Color.Black, OutlineColor = Color.White, Size = new Vector2(120, 30) };
        var stack = new StackPanelWidget { Direction = LayoutDirection.Vertical };
        var textBox = new TextBoxWidget
        {
            VerticalAlignment = WidgetAlignment.Center, Color = new Color(255, 255, 255), Margin = new Vector2(4f, 0f),
            Size = new Vector2(120, 30), MaximumLength = 256
        };
        textBox.FontScale = 0.7f;
        textBox.Text = text;
        textBox.TextChanged += ac;
        stack.Children.Add(textBox);
        canvasWidget.Children.Add(rectangleWidget);
        canvasWidget.Children.Add(stack);
        return canvasWidget;
    }

    private static Widget MakeButton(string txt, Action tas)
    {
        var clickTextWidget = new ClickTextWidget(new Vector2(120, 30), txt, tas);
        clickTextWidget.BorderColor = Color.White;
        clickTextWidget.Margin = new Vector2(0, 3);
        clickTextWidget.LabelWidget.FontScale = 0.7f;
        clickTextWidget.LabelWidget.Color = Color.Green;
        return clickTextWidget;
    }

    public override void Update()
    {
        if (Input.Back || Input.Cancel)
        {
            DialogsManager.HideDialog(this);
        }

        if (_isSetPos)
        {
            _list[_clickPos].BorderColor = Color.Red; //设定选择颜色
            return;
        }

        if (!_isClick)
        {
            return;
        }

        for (var i = 0; i < _list.Count; i++)
        {
            _list[i].BorderColor = i == _clickPos
                ? Color.Yellow //设定选择颜色
                : Color.Transparent; //设定选择颜色
            _list[i].LabelWidget.Text = $"{MemoryBankData.HexChars[Read(i)]}";
        }

        _isClick = false;
    }
}
