using System.Text;
using System.Xml.Linq;

using Engine.Graphics;
using Engine.Input;

namespace Game.Widgets;

public sealed class MemoryBankEditorWidget : CanvasWidget, IGameInputCapturingWidget
{
    private const int _cellsPerRow = 4;
    private const int _digitsPerCell = 4;
    private const int _digitsPerPage = 64;
    private const int _rowsPerPage = 4;
    private const string _languageSection = nameof(MemoryBankEditorWidget);

    private static readonly Key[] _digitKeys =
    [
        Key.Number0, Key.Number1, Key.Number2, Key.Number3, Key.Number4,
        Key.Number5, Key.Number6, Key.Number7, Key.Number8, Key.Number9,
        Key.A, Key.B, Key.C, Key.D, Key.E, Key.F
    ];

    private readonly StringBuilder _cellBuffer = new(_digitsPerCell);
    private readonly LabelWidget[,] _cellLabels = new LabelWidget[_rowsPerPage, _cellsPerRow];
    private readonly CanvasWidget[,] _inputFrames = new CanvasWidget[_rowsPerPage, _cellsPerRow];
    private readonly BevelledRectangleWidget[,] _inputBorders =
        new BevelledRectangleWidget[_rowsPerPage, _cellsPerRow];
    private readonly LabelWidget[,] _inputLabels = new LabelWidget[_rowsPerPage, _cellsPerRow];
    private readonly ButtonWidget[] _digitButtons = new ButtonWidget[16];
    private readonly Action<bool> _completed;
    private readonly Action _close;
    private readonly GridPanelWidget _keypad;
    private readonly ButtonWidget _nextButton;
    private readonly LabelWidget _pageLabel;
    private readonly ButtonWidget _previousButton;
    private readonly LabelWidget[] _rowLabels = new LabelWidget[_rowsPerPage];
    private readonly MemoryBankData _sourceData;
    private readonly StackPanelWidget _table;
    private ButtonWidget _backspaceButton = null!;
    private ButtonWidget _cancelButton = null!;
    private ButtonWidget _saveButton = null!;
    private ButtonWidget _tabButton = null!;
    private MemoryBankData _temporaryData;
    private bool _hasChanges;
    private int _page;
    private int _pageBufferLength;

    public MemoryBankEditorWidget(MemoryBankData data, Action<bool> completed, Action close)
    {
        LoadContents(this, ContentManager.Get<XElement>("Widgets/MemoryBankEditorWidget"));
        _sourceData = data;
        _temporaryData = (MemoryBankData)data.Copy();
        _completed = completed;
        _close = close;
        _previousButton = Children.Find<ButtonWidget>("MemoryBankEditor.Previous")!;
        _nextButton = Children.Find<ButtonWidget>("MemoryBankEditor.Next")!;
        _pageLabel = Children.Find<LabelWidget>("MemoryBankEditor.Page")!;
        _table = Children.Find<StackPanelWidget>("MemoryBankEditor.Table")!;
        _keypad = Children.Find<GridPanelWidget>("MemoryBankEditor.Keypad")!;
        CreateTable();
        CreateKeypad();
        Refresh();
    }

    public override void Update()
    {
        _saveButton.IsEnabled = _hasChanges;
        KeyboardInput.GetInput();
        for (var value = 0; value < _digitKeys.Length; value++)
        {
            if (Input.IsKeyDownOnce(_digitKeys[value]))
            {
                AppendDigit(MemoryBankData.HexChars[value]);
            }
        }

        for (var value = 0; value < _digitButtons.Length; value++)
        {
            if (_digitButtons[value].IsClicked)
            {
                AppendDigit(MemoryBankData.HexChars[value]);
            }
        }

        if (Input.IsKeyDownOnce(Key.Tab))
        {
            AdvanceBufferToNextCell();
        }

        if (_tabButton.IsClicked)
        {
            AdvanceBufferToNextCell();
        }

        if (KeyboardInput.BackspacePressed || Input.IsKeyDownRepeat(Key.BackSpace) || _backspaceButton.IsClicked)
        {
            Backspace();
        }

        if (_previousButton.IsClicked && _page > 0)
        {
            ChangePage(_page - 1);
        }

        if (_nextButton.IsClicked && _page < 3)
        {
            ChangePage(_page + 1);
        }

        if (_saveButton.IsClicked)
        {
            _sourceData.Data = new DynamicArray<byte>(_temporaryData.Data);
            _sourceData.LastOutput = _temporaryData.LastOutput;
            _completed(true);
            _close();
        }

        if (_cancelButton.IsClicked)
        {
            _completed(false);
            _close();
        }

    }

    private void CreateTable()
    {
        var header = CreateRowCanvas(32f);
        AddLabel(header, Text("Address"), 0f, 48f, Color.White);
        AddLabel(header, "0123", 50f, 130f, Color.White);
        AddLabel(header, "4567", 187f, 130f, Color.White);
        AddLabel(header, "89AB", 324f, 130f, Color.White);
        AddLabel(header, "CDEF", 461f, 130f, Color.White);
        _table.Children.Add(header);

        for (var row = 0; row < _rowsPerPage; row++)
        {
            var rowCanvas = CreateRowCanvas(38f);
            _rowLabels[row] = AddLabel(rowCanvas, string.Empty, 0f, 48f, Color.White);
            for (var column = 0; column < _cellsPerRow; column++)
            {
                CreateCell(rowCanvas, row, column, 50f + 137f * column);
            }

            _table.Children.Add(rowCanvas);
        }
    }

    private void CreateCell(CanvasWidget rowCanvas, int row, int column, float x)
    {
        var cell = new CanvasWidget { Size = new Vector2(130f, 36f) };
        rowCanvas.Children.Add(cell);
        SetPosition(cell, new Vector2(x, 1f));
        _cellLabels[row, column] = AddLabel(cell, string.Empty, 0f, 130f, Color.White);

        var inputFrame = new CanvasWidget { Size = new Vector2(130f, 36f) };
        var inputBorder = new BevelledRectangleWidget
        {
            CenterColor = Color.Black,
            BevelColor = Color.Gray,
            BevelSize = 1f
        };
        inputFrame.Children.Add(inputBorder);
        _inputBorders[row, column] = inputBorder;
        _inputLabels[row, column] = AddLabel(inputFrame, string.Empty, 0f, 130f, Color.White);
        cell.Children.Add(inputFrame);
        _inputFrames[row, column] = inputFrame;

        var selectedRow = row;
        var selectedColumn = column;
        var clickable = new ClickableWidget { SoundName = "Audio/UI/ButtonClick" };
        clickable.OnClick = () => SelectCell(selectedRow, selectedColumn);
        cell.Children.Add(clickable);
    }

    private void CreateKeypad()
    {
        for (var value = 0; value < _digitButtons.Length; value++)
        {
            var button = CreateKeyButton(MemoryBankData.HexChars[value].ToString());
            _digitButtons[value] = button;
            AddKeyButton(button, value < 10 ? value : value - 10, value < 10 ? 0 : 1);
        }

        _tabButton = CreateKeyButton("Tab");
        _backspaceButton = CreateKeyButton("<");
        _saveButton = CreateKeyButton(Text("Save"));
        _cancelButton = CreateKeyButton(Text("Cancel"));
        AddKeyButton(_tabButton, 6, 1);
        AddKeyButton(_backspaceButton, 7, 1);
        AddKeyButton(_saveButton, 8, 1);
        AddKeyButton(_cancelButton, 9, 1);
    }

    private void AppendDigit(char digit)
    {
        _cellBuffer.Append(digit);
        _pageBufferLength++;
        WriteBufferedCell();
        _hasChanges = true;
        if (_cellBuffer.Length >= _digitsPerCell)
        {
            _cellBuffer.Clear();
        }

        AdvancePageIfNeeded();
        Refresh();
    }

    private void AdvanceBufferToNextCell()
    {
        var digitsToSkip = _digitsPerCell - _pageBufferLength % _digitsPerCell;
        _pageBufferLength += digitsToSkip;
        _cellBuffer.Clear();
        AdvancePageIfNeeded();
        Refresh();
    }

    private void Backspace()
    {
        if (_pageBufferLength == 0)
        {
            _page = (_page + 3) % 4;
            _pageBufferLength = _digitsPerPage;
        }

        if (_cellBuffer.Length == 0)
        {
            var previousCell = (_pageBufferLength - 1) / _digitsPerCell;
            var addressRow = _page * _rowsPerPage + previousCell / _cellsPerRow;
            var column = previousCell % _cellsPerRow;
            _cellBuffer.Append(ReadGroup(addressRow, column));
        }

        _cellBuffer.Length--;
        _pageBufferLength--;
        WriteCellFromBuffer(_pageBufferLength / _digitsPerCell);
        _hasChanges = true;
        Refresh();
    }

    private void AdvancePageIfNeeded()
    {
        if (_pageBufferLength < _digitsPerPage)
        {
            return;
        }

        _page = (_page + 1) % 4;
        _pageBufferLength = 0;
        _cellBuffer.Clear();
    }

    private void WriteBufferedCell()
    {
        var cellIndex = (_pageBufferLength - 1) / _digitsPerCell;
        WriteCellFromBuffer(cellIndex);
    }

    private void WriteCellFromBuffer(int cellIndex)
    {
        var addressRow = _page * _rowsPerPage + cellIndex / _cellsPerRow;
        var column = cellIndex % _cellsPerRow;
        var value = _cellBuffer.ToString().PadLeft(_digitsPerCell, '0');
        for (var offset = 0; offset < _digitsPerCell; offset++)
        {
            _temporaryData.Write(addressRow * 16 + column * 4 + offset,
                (byte)MemoryBankData.HexChars.IndexOf(value[offset]));
        }
    }

    private void SelectCell(int row, int column)
    {
        _pageBufferLength = (row * _cellsPerRow + column) * _digitsPerCell;
        _cellBuffer.Clear();
        Refresh();
    }

    private void ChangePage(int page)
    {
        _page = page;
        _pageBufferLength = 0;
        _cellBuffer.Clear();
        Refresh();
    }

    private void Refresh()
    {
        _pageLabel.Text = $"{_page + 1} / 4";
        _previousButton.IsEnabled = _page > 0;
        _nextButton.IsEnabled = _page < 3;
        var activeCell = MathUtils.Min(_pageBufferLength / _digitsPerCell, 15);
        var activeRow = activeCell / _cellsPerRow;
        var activeColumn = activeCell % _cellsPerRow;
        for (var row = 0; row < _rowsPerPage; row++)
        {
            var addressRow = _page * _rowsPerPage + row;
            var isActiveRow = row == activeRow;
            _rowLabels[row].Text = MemoryBankData.HexChars[addressRow].ToString();
            for (var column = 0; column < _cellsPerRow; column++)
            {
                var value = ReadGroup(addressRow, column);
                _cellLabels[row, column].Text = value;
                _cellLabels[row, column].IsVisible = !isActiveRow;
                _inputFrames[row, column].IsVisible = isActiveRow;
                _inputLabels[row, column].Text = value;
                var isActiveCell = isActiveRow && column == activeColumn;
                _inputBorders[row, column].BevelColor = isActiveCell ? new Color(80, 200, 40) : Color.Gray;
                _inputBorders[row, column].BevelSize = isActiveCell ? 2f : 1f;
            }
        }
    }

    private string ReadGroup(int addressRow, int column)
    {
        var builder = new StringBuilder(_digitsPerCell);
        for (var offset = 0; offset < _digitsPerCell; offset++)
        {
            builder.Append(MemoryBankData.HexChars[_temporaryData.Read(addressRow * 16 + column * 4 + offset)]);
        }

        return builder.ToString();
    }

    private static CanvasWidget CreateRowCanvas(float height)
    {
        return new CanvasWidget { Size = new Vector2(600f, height) };
    }

    private static BevelledButtonWidget CreateKeyButton(string text)
    {
        return new BevelledButtonWidget
        {
            Text = text,
            Size = new Vector2(58f, 54f),
            Margin = new Vector2(1f),
            FontScale = 0.72f
        };
    }

    private void AddKeyButton(ButtonWidget button, int column, int row)
    {
        _keypad.Children.Add(button);
        _keypad.SetWidgetCell(button, new Point2(column, row));
    }

    private static LabelWidget AddLabel(ContainerWidget parent, string text, float x, float width, Color color)
    {
        var label = new LabelWidget
        {
            Text = text,
            Size = new Vector2(width, 36f),
            FontScale = 0.9f,
            Color = color,
            HorizontalAlignment = WidgetAlignment.Center,
            VerticalAlignment = WidgetAlignment.Center,
            TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter
        };
        parent.Children.Add(label);
        SetPosition(label, new Vector2(x, 0f));
        return label;
    }

    private static string Text(string key)
    {
        return LanguageManager.GetContentWidgets(_languageSection, key);
    }
}
