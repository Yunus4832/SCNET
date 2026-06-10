using System.Xml.Linq;

namespace Game.Dialogs;

public class BuildFurnitureDialog : Dialog
{
    private const string _typeName = "BuildFurnitureDialog";

    private int _axis;

    private readonly ButtonWidget _axisButton;

    private readonly ButtonWidget _buildButton;

    private readonly ButtonWidget _cancelButton;

    private readonly ButtonWidget _decreaseResolutionButton;

    private readonly FurnitureDesign _design;

    private readonly FurnitureDesignWidget _designWidget2D;

    private readonly FurnitureDesignWidget _designWidget3D;

    private readonly ButtonWidget _downButton;

    private readonly Action<bool> _handler;

    private readonly ButtonWidget _increaseResolutionButton;

    private readonly bool _isValid;

    private readonly ButtonWidget _leftButton;

    private readonly ButtonWidget _mirrorButton;

    private readonly ButtonWidget _nameButton;

    private readonly LabelWidget _nameLabel;

    private readonly LabelWidget _resolutionLabel;

    private readonly ButtonWidget _rightButton;

    private readonly FurnitureDesign? _sourceDesign;

    private readonly LabelWidget _statusLabel;

    private readonly ButtonWidget _turnRightButton;

    private readonly ButtonWidget _upButton;

    public BuildFurnitureDialog(FurnitureDesign design, FurnitureDesign? sourceDesign, Action<bool> handler)
    {
        var node = ContentManager.Get<XElement>("Dialogs/BuildFurnitureDialog");
        LoadContents(this, node);
        _nameLabel = Children.Find<LabelWidget>("BuildFurnitureDialog.Name")!;
        _statusLabel = Children.Find<LabelWidget>("BuildFurnitureDialog.Status")!;
        _designWidget2D = Children.Find<FurnitureDesignWidget>("BuildFurnitureDialog.Design2d")!;
        _designWidget3D = Children.Find<FurnitureDesignWidget>("BuildFurnitureDialog.Design3d")!;
        _nameButton = Children.Find<ButtonWidget>("BuildFurnitureDialog.NameButton")!;
        _axisButton = Children.Find<ButtonWidget>("BuildFurnitureDialog.AxisButton")!;
        _leftButton = Children.Find<ButtonWidget>("BuildFurnitureDialog.LeftButton")!;
        _rightButton = Children.Find<ButtonWidget>("BuildFurnitureDialog.RightButton")!;
        _upButton = Children.Find<ButtonWidget>("BuildFurnitureDialog.UpButton")!;
        _downButton = Children.Find<ButtonWidget>("BuildFurnitureDialog.DownButton")!;
        _mirrorButton = Children.Find<ButtonWidget>("BuildFurnitureDialog.MirrorButton")!;
        _turnRightButton = Children.Find<ButtonWidget>("BuildFurnitureDialog.TurnRightButton")!;
        _increaseResolutionButton = Children.Find<ButtonWidget>("BuildFurnitureDialog.IncreaseResolutionButton")!;
        _decreaseResolutionButton = Children.Find<ButtonWidget>("BuildFurnitureDialog.DecreaseResolutionButton")!;
        _resolutionLabel = Children.Find<LabelWidget>("BuildFurnitureDialog.ResolutionLabel")!;
        _cancelButton = Children.Find<ButtonWidget>("BuildFurnitureDialog.CancelButton")!;
        _buildButton = Children.Find<ButtonWidget>("BuildFurnitureDialog.BuildButton")!;
        _handler = handler;
        _design = design;
        _sourceDesign = sourceDesign;
        _axis = 1;
        var num = 0;
        num += _design.Geometry.SubsetOpaqueByFace.Sum(b => b != null ? b.Indices.Count / 3 : 0);
        num += _design.Geometry.SubsetAlphaTestByFace.Sum(b => b != null ? b.Indices.Count / 3 : 0);
        _isValid = num <= 65535;
        _statusLabel.Text = string.Format(LanguageManager.Get(_typeName, 1), num, 65535,
            _isValid ? LanguageManager.Get(_typeName, 2) : LanguageManager.Get(_typeName, 3));
        _designWidget2D.Design = _design;
        _designWidget3D.Design = _design;
    }

    public override void Update()
    {
        _nameLabel.Text = string.IsNullOrEmpty(_design.Name) ? _design.GetDefaultName() : _design.Name;
        _designWidget2D.Mode = (FurnitureDesignWidget.ViewMode)_axis;
        _designWidget3D.Mode = FurnitureDesignWidget.ViewMode.Perspective;
        if (_designWidget2D.Mode == FurnitureDesignWidget.ViewMode.Side)
        {
            _axisButton.Text = LanguageManager.Get(_typeName, 4);
        }

        if (_designWidget2D.Mode == FurnitureDesignWidget.ViewMode.Top)
        {
            _axisButton.Text = LanguageManager.Get(_typeName, 5);
        }

        if (_designWidget2D.Mode == FurnitureDesignWidget.ViewMode.Front)
        {
            _axisButton.Text = LanguageManager.Get(_typeName, 6);
        }

        _leftButton.IsEnabled = IsShiftPossible(DirectionAxisToDelta(0, _axis));
        _rightButton.IsEnabled = IsShiftPossible(DirectionAxisToDelta(1, _axis));
        _upButton.IsEnabled = IsShiftPossible(DirectionAxisToDelta(2, _axis));
        _downButton.IsEnabled = IsShiftPossible(DirectionAxisToDelta(3, _axis));
        _decreaseResolutionButton.IsEnabled = IsDecreaseResolutionPossible();
        _increaseResolutionButton.IsEnabled = IsIncreaseResolutionPossible();
        _resolutionLabel.Text = $"{_design.Resolution}";
        _buildButton.IsEnabled = _isValid;
        if (_nameButton.IsClicked)
        {
            var list = new List<Tuple<string, Action>>();
            if (_sourceDesign != null)
            {
                list.Add(new Tuple<string, Action>(LanguageManager.Get(_typeName, 7), delegate
                {
                    Dismiss(false);
                    DialogsManager.ShowDialog(ParentWidget,
                        new TextBoxDialog(
                            LanguageManager.Get(_typeName, 10),
                            _sourceDesign.Name,
                            20,
                            delegate(string s)
                            {
                                try
                                {
                                    _sourceDesign.Name = s;
                                }
                                catch (Exception ex3)
                                {
                                    DialogsManager.Alert(ex3.Message);
                                }
                            }
                        )
                    );
                }));
                list.Add(new Tuple<string, Action>(LanguageManager.Get(_typeName, 8), delegate
                {
                    DialogsManager.ShowDialog(
                        ParentWidget,
                        new TextBoxDialog(LanguageManager.Get(_typeName, 11),
                            _design.Name,
                            20,
                            delegate(string s)
                            {
                                try
                                {
                                    _design.Name = s;
                                }
                                catch (Exception ex2)
                                {
                                    DialogsManager.Alert(ex2.Message);
                                }
                            }
                        )
                    );
                }));
            }
            else
            {
                list.Add(new Tuple<string, Action>(LanguageManager.Get(_typeName, 9), delegate
                {
                    DialogsManager.ShowDialog(
                        ParentWidget,
                        new TextBoxDialog(LanguageManager.Get(_typeName, 11),
                            _design.Name,
                            20,
                            delegate(string s)
                            {
                                try
                                {
                                    _design.Name = s;
                                }
                                catch (Exception ex)
                                {
                                    DialogsManager.Alert(ex.Message);
                                }
                            }
                        )
                    );
                }));
            }

            if (list.Count == 1)
            {
                list[0].Item2();
            }
            else
            {
                DialogsManager.ShowDialog(
                    ParentWidget,
                    new ListSelectionDialog(
                        LanguageManager.Get(_typeName, 11),
                        list,
                        64f,
                        t => ((Tuple<string, Action>)t).Item1,
                        delegate(object t) { ((Tuple<string, Action>)t).Item2(); }
                    )
                );
            }
        }

        if (_axisButton.IsClicked)
        {
            _axis = (_axis + 1) % 3;
        }

        if (_leftButton.IsClicked)
        {
            Shift(DirectionAxisToDelta(0, _axis));
        }

        if (_rightButton.IsClicked)
        {
            Shift(DirectionAxisToDelta(1, _axis));
        }

        if (_upButton.IsClicked)
        {
            Shift(DirectionAxisToDelta(2, _axis));
        }

        if (_downButton.IsClicked)
        {
            Shift(DirectionAxisToDelta(3, _axis));
        }

        if (_mirrorButton.IsClicked)
        {
            _design.Mirror(_axis);
        }

        if (_turnRightButton.IsClicked)
        {
            _design.Rotate(_axis, 1);
        }

        if (_decreaseResolutionButton.IsClicked)
        {
            DecreaseResolution();
        }

        if (_increaseResolutionButton.IsClicked)
        {
            IncreaseResolution();
        }

        if (_buildButton.IsClicked && _isValid)
        {
            Dismiss(true);
        }

        if (Input.Back || _cancelButton.IsClicked)
        {
            Dismiss(false);
        }
    }

    public bool IsShiftPossible(Point3 delta)
    {
        var resolution = _design.Resolution;
        var box = _design.Box;
        box.Location += delta;
        return box.Left >= 0 &&
               box.Top >= 0 &&
               box.Near >= 0 &&
               box.Right <= resolution &&
               box.Bottom <= resolution &&
               box.Far <= resolution;
    }

    public void Shift(Point3 delta)
    {
        if (IsShiftPossible(delta))
        {
            _design.Shift(delta);
        }
    }

    public bool IsDecreaseResolutionPossible()
    {
        var resolution = _design.Resolution;
        if (resolution <= 2)
        {
            return false;
        }

        var num = MathUtils.Max(_design.Box.Width, _design.Box.Height, _design.Box.Depth);
        return resolution > num;
    }

    public void DecreaseResolution()
    {
        if (!IsDecreaseResolutionPossible())
        {
            return;
        }

        var resolution = _design.Resolution;
        var zero = Point3.Zero;
        if (_design.Box.Right >= resolution)
        {
            zero.X = -1;
        }

        if (_design.Box.Bottom >= resolution)
        {
            zero.Y = -1;
        }

        if (_design.Box.Far >= resolution)
        {
            zero.Z = -1;
        }

        _design.Shift(zero);
        _design.Resize(resolution - 1);
    }

    public bool IsIncreaseResolutionPossible()
    {
        return _design.Resolution < 16;
    }

    public void IncreaseResolution()
    {
        if (IsIncreaseResolutionPossible())
        {
            _design.Resize(_design.Resolution + 1);
        }
    }

    public static Point3 DirectionAxisToDelta(int direction, int axis)
    {
        return direction switch
        {
            0 => axis switch
            {
                0 => new Point3(0, 0, 1),
                1 or 2 => new Point3(1, 0, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
            },
            1 => axis switch
            {
                0 => new Point3(0, 0, -1),
                1 or 2 => new Point3(-1, 0, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
            },
            2 => axis switch
            {
                0 => new Point3(0, 1, 0),
                1 => new Point3(0, 0, 1),
                2 => new Point3(0, 1, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
            },
            3 => axis switch
            {
                0 => new Point3(0, -1, 0),
                1 => new Point3(0, 0, -1),
                2 => new Point3(0, -1, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
            },
            _ => Point3.Zero
        };
    }

    public void Dismiss(bool result)
    {
        DialogsManager.HideDialog(this);
        _handler(result);
    }
}
