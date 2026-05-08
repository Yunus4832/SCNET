namespace Game.NetWork;

public class HalfFloat
{
    private static readonly int _expSize = 6; //指数的位数
    private static readonly int _mSize = 15 - _expSize; //尾数的位数

    public static byte[] FloatToHalf(float f)
    {
        var bytes = BitConverter.GetBytes(f);
        byte sign = 0x80;
        var myByte = new byte[2]; //返回的数组

        sign = (byte)(bytes[3] & sign); //求符号位
        //求指数位
        var exp = (sbyte)(bytes[3] << 1);
        exp += (sbyte)(bytes[2] >> 7);
        exp -= 127;
        exp += (sbyte)((1 << (_expSize - 1)) - 1);
        if (exp < 0) //下溢出
        {
            exp = 0;
        }

        //求尾数
        var m = (ushort)(bytes[2] & 0x7f);
        m = (ushort)(m << (_mSize - 7));
        m += (ushort)(bytes[1] >> (15 - _mSize));
        if (((bytes[1] >> (15 - _mSize - 1)) & 1) == 1) //若被移除的最高位是1，则产生进位。
        {
            m += 1;
        }

        if (m >= (ushort)Math.Pow(2, _mSize)) //若进位后发生尾数溢出，则取消进位
        {
            m -= 1;
        }

        ushort result = sign;
        result = (ushort)(result << 8); //把符号位移动到最高位上
        //装载指数位
        short temp1 = exp;
        temp1 = (short)(temp1 << (15 - _expSize));
        result += (ushort)temp1;
        result += m; //装载尾数
        myByte[0] = (byte)result;
        myByte[1] = (byte)(result >> 8);
        return myByte;
    }


    public static float HalfToFloat(byte[] myByte)
    {
        ushort h = myByte[1];
        h = (ushort)(h << 8);
        h += myByte[0];
        var sign = 1;
        double temp = 0;
        if (h >> 15 == 1)
        {
            sign = -1;
        }

        var exp = h & 0x00007fff;
        exp = exp >> _mSize; //提取指数位
        exp -= (1 << (_expSize - 1)) - 1;
        var m = (uint)(h << (_expSize + 1)) >> (_expSize + 1);
        for (var i = 0; i < _mSize; i++)
        {
            if ((m & 1) == 1)
            {
                temp += Math.Pow(2, i - _mSize);
            }

            m = m >> 1;
        }

        temp += 1;
        temp *= Math.Pow(2, exp);
        var result = (float)temp;
        return result * sign;
    }
}
