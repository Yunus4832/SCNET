using System.Runtime.InteropServices;
using Engine.Core;

namespace Engine.Media;

public static class Bmp
{
    public enum Format
    {
        RGBA8,
        RGB8
    }

    public static bool IsBmpStream(Stream stream)
    {
        var position = stream.Position;
        var num = stream.ReadByte();
        var num2 = stream.ReadByte();
        stream.Position = position;
        if (num == 66)
        {
            return num2 == 77;
        }

        return false;
    }

    public static BmpInfo GetInfo(Stream stream)
    {
        var bitmapHeader = ReadHeader(stream);
        var result = default(BmpInfo);
        result.Width = bitmapHeader.Width;
        result.Height = bitmapHeader.Height;
        if (bitmapHeader.BitCount == 32)
        {
            result.Format = Format.RGBA8;
        }
        else
        {
            if (bitmapHeader.BitCount != 24)
            {
                throw new InvalidOperationException("Unsupported BMP pixel format.");
            }

            result.Format = Format.RGB8;
        }

        return result;
    }

    public static Image Load(Stream stream)
    {
        var bitmapHeader = ReadHeader(stream);
        var image = new Image(bitmapHeader.Width, MathUtils.Abs(bitmapHeader.Height));
        if (bitmapHeader.BitCount == 32)
        {
            var array = new byte[4 * image.Width];
            for (var i = 0; i < image.Height; i++)
            {
                if (stream.Read(array, 0, array.Length) != array.Length)
                {
                    throw new InvalidOperationException("BMP data truncated.");
                }

                var num = bitmapHeader.Height < 0 ? image.Width * (image.Height - i - 1) : image.Width * i;
                var j = 0;
                var num2 = 0;
                for (; j < image.Width; j++)
                {
                    var b = array[num2++];
                    var g = array[num2++];
                    var r = array[num2++];
                    var a = array[num2++];
                    image.Pixels[num++] = new Color(r, g, b, a);
                }
            }
        }
        else
        {
            if (bitmapHeader.BitCount != 24)
            {
                throw new InvalidOperationException("Unsupported BMP pixel format.");
            }

            var array2 = new byte[(3 * image.Width + 3) / 4 * 4];
            for (var k = 0; k < image.Height; k++)
            {
                if (stream.Read(array2, 0, array2.Length) != array2.Length)
                {
                    throw new InvalidOperationException("BMP data truncated.");
                }

                var num3 = bitmapHeader.Height < 0 ? image.Width * (image.Height - k - 1) : image.Width * k;
                var l = 0;
                var num4 = 0;
                for (; l < image.Width; l++)
                {
                    var b2 = array2[num4++];
                    var g2 = array2[num4++];
                    var r2 = array2[num4++];
                    image.Pixels[num3++] = new Color(r2, g2, b2);
                }
            }
        }

        return image;
    }

    public static void Save(Image image, Stream stream, Format format)
    {
        var structure = default(BitmapHeader);
        structure.Type1 = 66;
        structure.Type2 = 77;
        structure.Reserved1 = 0;
        structure.Reserved2 = 0;
        structure.OffBits = 54;
        structure.Size2 = 40;
        structure.Width = image.Width;
        structure.Height = -image.Height;
        structure.Planes = 1;
        structure.Compression = 0;
        structure.SizeImage = 0;
        structure.XPelsPerMeter = 3780;
        structure.YPelsPerMeter = 3780;
        structure.ClrUsed = 0;
        structure.ClrImportant = 0;
        if (format == Format.RGBA8)
        {
            structure.Size = 54 + 4 * image.Width * image.Height;
            structure.BitCount = 32;
        }
        else
        {
            structure.Size = 54 + (3 * image.Width + 3) / 4 * 4 * image.Height;
            structure.BitCount = 24;
        }

        var array = Utilities.StructureToArray(structure);
        stream.Write(array, 0, array.Length);
        if (format == Format.RGBA8)
        {
            var array2 = new byte[4 * image.Width];
            for (var i = 0; i < image.Height; i++)
            {
                var num = image.Width * i;
                var j = 0;
                var num2 = 0;
                for (; j < image.Width; j++)
                {
                    var color = image.Pixels[num++];
                    array2[num2++] = color.B;
                    array2[num2++] = color.G;
                    array2[num2++] = color.R;
                    array2[num2++] = color.A;
                }

                stream.Write(array2, 0, array2.Length);
            }

            return;
        }

        var array3 = new byte[(3 * image.Width + 3) / 4 * 4];
        for (var k = 0; k < image.Height; k++)
        {
            var num3 = image.Width * k;
            var l = 0;
            var num4 = 0;
            for (; l < image.Width; l++)
            {
                var color2 = image.Pixels[num3++];
                array3[num4++] = color2.B;
                array3[num4++] = color2.G;
                array3[num4++] = color2.R;
            }

            stream.Write(array3, 0, array3.Length);
        }
    }

    private static BitmapHeader ReadHeader(Stream stream)
    {
        if (!BitConverter.IsLittleEndian)
        {
            throw new InvalidOperationException("Unsupported system endianness.");
        }

        var array = new byte[54];
        if (stream.Read(array, 0, array.Length) != array.Length)
        {
            throw new InvalidOperationException("Invalid BMP header.");
        }

        var result = Utilities.ArrayToStructure<BitmapHeader>(array);
        if (result.Type1 != 66 || result.Type2 != 77)
        {
            throw new InvalidOperationException("Invalid BMP header.");
        }

        return result.Compression != 0 ? throw new InvalidOperationException("Unsupported BMP compression.") : result;
    }

    public struct BmpInfo
    {
        public int Width;

        public int Height;

        public Format Format;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BitmapHeader
    {
        public byte Type1;

        public byte Type2;

        public int Size;

        public short Reserved1;

        public short Reserved2;

        public int OffBits;

        public int Size2;

        public int Width;

        public int Height;

        public short Planes;

        public short BitCount;

        public int Compression;

        public int SizeImage;

        public int XPelsPerMeter;

        public int YPelsPerMeter;

        public int ClrUsed;

        public int ClrImportant;
    }
}
