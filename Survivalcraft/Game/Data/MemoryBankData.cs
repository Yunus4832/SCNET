using System.Text;

namespace Game;

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
                var num = HexChars.IndexOf(char.ToUpperInvariant(text[i]));
                if (num < 0)
                {
                    num = 0;
                }

                Data.Add((byte)num);
            }
        }

        if (array.Length < 2)
        {
            return;
        }

        var text2 = array[1];
        var num2 = HexChars.IndexOf(char.ToUpperInvariant(text2[0]));
        if (num2 < 0)
        {
            num2 = 0;
        }

        LastOutput = (byte)num2;
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
        var num = 0;
        for (var i = 0; i < Data.Count; i++)
        {
            if (Data.Array[i] != 0)
            {
                num = i + 1;
            }
        }

        for (var j = 0; j < num; j++)
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
