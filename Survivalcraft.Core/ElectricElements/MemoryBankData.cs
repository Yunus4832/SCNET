using System.Text;

namespace Game.ElectricElements;

public class MemoryBankData : IEditableItemData
{
    public static readonly List<char> HexChars =
    [
        '0',
        '1',
        '2',
        '3',
        '4',
        '5',
        '6',
        '7',
        '8',
        '9',
        'A',
        'B',
        'C',
        'D',
        'E',
        'F'
    ];

    public DynamicArray<byte> Data = [];

    public byte LastOutput { get; set; }

    public IEditableItemData Copy()
    {
        return new MemoryBankData
        {
            Data = new DynamicArray<byte>(Data),
            LastOutput = LastOutput
        };
    }

    public void LoadString(string data)
    {
        var array = data.Split([';'], StringSplitOptions.RemoveEmptyEntries);
        if (array.Length >= 1)
        {
            var text = array[0];
            text = text.TrimEnd('0');
            Data.Clear();
            for (var i = 0; i < MathUtils.Min(text.Length, 256); i++)
            {
                var hexDigit = HexChars.IndexOf(char.ToUpperInvariant(text[i]));
                if (hexDigit < 0)
                {
                    hexDigit = 0;
                }

                Data.Add((byte)hexDigit);
            }
        }

        if (array.Length < 2)
        {
            return;
        }

        var text2 = array[1];
        var outputDigit = HexChars.IndexOf(char.ToUpperInvariant(text2[0]));
        if (outputDigit < 0)
        {
            outputDigit = 0;
        }

        LastOutput = (byte)outputDigit;
    }

    public string SaveString()
    {
        return SaveString(true);
    }

    public byte Read(int address)
    {
        if (address >= 0 && address < Data.Count)
        {
            return Data.Array[address];
        }

        return 0;
    }

    public void Write(int address, byte data)
    {
        if (address >= 0 && address < Data.Count)
        {
            Data.Array[address] = data;
        }
        else if (address is >= 0 and < 256 && data != 0)
        {
            Data.Count = MathUtils.Max(Data.Count, address + 1);
            Data.Array[address] = data;
        }
    }

    public string SaveString(bool saveLastOutput)
    {
        var stringBuilder = new StringBuilder();
        var usedLength = 0;
        for (var i = 0; i < Data.Count; i++)
        {
            if (Data.Array[i] != 0)
            {
                usedLength = i + 1;
            }
        }

        for (var j = 0; j < usedLength; j++)
        {
            var index = MathUtils.Clamp(Data.Array[j], 0, 15);
            stringBuilder.Append(HexChars[index]);
        }

        if (!saveLastOutput)
        {
            return stringBuilder.ToString();
        }

        stringBuilder.Append(';');
        stringBuilder.Append(HexChars[MathUtils.Clamp(LastOutput, 0, 15)]);

        return stringBuilder.ToString();
    }
}
