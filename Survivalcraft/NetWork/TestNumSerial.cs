namespace Game.NetWork;

public class TestNumSerial
{
    public static byte[] SerialFloat(float f)
    {
        byte[] data = [];
        var p = (int)f; //获取整数位
        var remain = f - p; //小数位
        var d = (int)(remain * 1000000); //获取小数后6位

        return data;
    }

    public static void DeSerialFloat()
    {
    }

    public static byte[] SerialInt(int v)
    {
        var buf = new List<byte>();
        byte dataByte = 0; //数据位
        if (v < 0)
        {
            dataByte |= 0x80; //符号位
        }

        var vv = (uint)(v & 0x7fffffff);
        if (vv <= 0xff)
        {
            dataByte |= 1;
        }
        else if (vv <= 0xffff)
        {
            dataByte |= 2;
        }
        else if (vv <= 0xffffff)
        {
            dataByte |= 3;
        }
        else
        {
            dataByte |= 4;
        }

        return buf.ToArray();
    }

    public static int DeSerialInt(byte[] buf)
    {
        var data = CommonLib.Decompress(buf);
        var v = 0;
        for (var i = data.Length - 1; i >= 0; i--)
        {
            v |= data[i];
            v <<= 8;
        }

        return v;
    }
}
