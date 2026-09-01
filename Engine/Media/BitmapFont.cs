using Engine.Core;
using Engine.FileStorage;
using Engine.Graphics;

namespace Engine.Media;

public class BitmapFont : IDisposable
{
    private static BitmapFont? _debugFont;

    internal Glyph[] glyphsByCode = [];

    internal Image? image;

    static BitmapFont()
    {
        Display.DeviceReset += delegate
        {
            if (_debugFont != null)
            {
                return;
            }

            using var stream = typeof(BitmapFont).GetTypeInfo().Assembly
                .GetManifestResourceStream("Engine.Resources.Debugfont.png");
            if (stream is null)
            {
                throw new InvalidOperationException("Engine.Resources.DebugFont.png not found");
            }

            using var stream2 = typeof(BitmapFont).GetTypeInfo().Assembly
                .GetManifestResourceStream("Engine.Resources.Debugfont.lst");
            if (stream2 is null)
            {
                throw new InvalidOperationException("Engine.Resources.DebugFont.lst not found");
            }

            _debugFont = Initialize(stream, stream2);
        };
    }

    public BitmapFont(Texture2D texture, IEnumerable<Glyph> glyphs, char fallbackCode, float glyphHeight,
        Vector2 spacing, float scale)
    {
        Initialize(texture, null, glyphs, fallbackCode, glyphHeight, spacing, scale);
    }

    internal BitmapFont()
    {
    }

    public Texture2D? Texture { get; private set; }

    public float GlyphHeight { get; private set; }

    public float LineHeight { get; private set; }

    public Vector2 Spacing { get; private set; }

    public float Scale { get; private set; }

    public Glyph FallbackGlyph { get; private set; } = null!;

    public char MaxGlyphCode { get; private set; }

    public static BitmapFont DebugFont
    {
        get
        {
            if (_debugFont is not null)
            {
                return _debugFont;
            }

            using var stream = typeof(BitmapFont).GetTypeInfo().Assembly
                .GetManifestResourceStream("Engine.Resources.DebugFont.png");
            if (stream is null)
            {
                throw new InvalidOperationException("Engine.Resources.DebugFont.png not found");
            }

            using var stream2 = typeof(BitmapFont).GetTypeInfo().Assembly
                .GetManifestResourceStream("Engine.Resources.DebugFont.lst");
            if (stream2 is null)
            {
                throw new InvalidOperationException("Engine.Resources.DebugFont.lst not found");
            }

            _debugFont = Initialize(stream, stream2);

            return _debugFont;
        }
    }

    public void Dispose()
    {
        Texture?.Dispose();
        Texture = null!;
    }

    /// <summary>
    ///     纹理图
    /// </summary>
    public static BitmapFont Initialize(Stream textureStream, Stream glyphsStream, Vector2? customGlyphOffset = null)
    {
        try
        {
            var texture = Texture2D.Load(textureStream);
            var bitmapFont = new BitmapFont();
            var streamReader = new StreamReader(glyphsStream);
            var num = int.Parse(streamReader.ReadLine() ?? string.Empty);
            var array = new Glyph[num];
            for (var i = 0; i < num; i++)
            {
                var line = streamReader.ReadLine() ?? string.Empty;
                var arr = line.Split([(char)0x20, (char)0x09], StringSplitOptions.None);
                if (arr.Length == 9)
                {
                    var tmp = new string[8];
                    tmp[0] = " ";
                    for (var j = 2; j < arr.Length; j++)
                    {
                        tmp[j - 1] = arr[j];
                    }

                    arr = tmp;
                }

                var code = char.Parse(arr[0]);
                var texCoord = new Vector2(float.Parse(arr[1]), float.Parse(arr[2]));
                var texCoord2 = new Vector2(float.Parse(arr[3]), float.Parse(arr[4]));
                var offset = new Vector2(float.Parse(arr[5]), float.Parse(arr[6]));
                if (customGlyphOffset.HasValue)
                {
                    offset += customGlyphOffset.Value;
                }

                var width = float.Parse(arr[7]);
                array[i] = new Glyph(code, texCoord, texCoord2, offset, width);
            }

            var glyphHeight = float.Parse(streamReader.ReadLine() ?? string.Empty);
            var line2 = streamReader.ReadLine() ?? string.Empty;
            var arr2 = line2.Split([(char)0x20, (char)0x09], StringSplitOptions.None);
            var spacing = new Vector2(float.Parse(arr2[0]), float.Parse(arr2[1]));
            var scale = float.Parse(streamReader.ReadLine() ?? string.Empty);
            var fallbackCode = char.Parse(streamReader.ReadLine() ?? string.Empty);
            bitmapFont.Initialize(texture, null, array, fallbackCode, glyphHeight, spacing, scale);
            return bitmapFont;
        }
        catch (Exception e)
        {
            Log.Error(e.Message);
            throw;
        }
    }

    public Glyph GetGlyph(char code)
    {
        return code >= glyphsByCode.Length ? FallbackGlyph : glyphsByCode[code];
    }

    public Vector2 MeasureText(string text, Vector2 scale, Vector2 spacing)
    {
        return MeasureText(text, 0, text.Length, scale, spacing);
    }

    public Vector2 MeasureText(string text, int start, int length, Vector2 scale, Vector2 spacing)
    {
        scale *= Scale;
        spacing += Spacing;
        var vector = new Vector2(0f, (GlyphHeight + spacing.Y) * scale.Y);
        var result = vector;
        for (var i = start; i < start + length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '\n':
                    vector.X = 0f;
                    vector.Y += (GlyphHeight + spacing.Y) * scale.Y;
                    if (vector.Y > result.Y)
                    {
                        result.Y = vector.Y;
                    }

                    break;
                default:
                {
                    var glyph = GetGlyph(c);
                    vector.X += (glyph.Width + spacing.X) * scale.X;
                    if (vector.X > result.X)
                    {
                        result.X = vector.X;
                    }

                    break;
                }
                case '\r':
                    break;
            }
        }

        return result;
    }

    public int FitText(float width, string text, float scale, float spacing)
    {
        return FitText(width, text, 0, text.Length, scale, spacing);
    }

    public int FitText(float width, string text, int start, int length, float scale, float spacing)
    {
        scale *= Scale;
        spacing += Spacing.X;
        var num = 0f;
        for (var i = start; i < start + length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '\n':
                    num = 0f;
                    continue;
                case '\r':
                    continue;
            }

            var glyph = GetGlyph(c);
            num += (glyph.Width + spacing) * scale;
            if (num > width)
            {
                return i - start;
            }
        }

        return length;
    }

    public float CalculateCharacterPosition(string text, int characterIndex, Vector2 scale, Vector2 spacing)
    {
        characterIndex = MathUtils.Clamp(characterIndex, 0, text.Length);
        return MeasureText(text, 0, characterIndex, scale, spacing).X;
    }

    public static BitmapFont Load(Image image, char firstCode, char fallbackCode, Vector2 spacing, float scale,
        Vector2 offset, int mipLevelsCount = 1, bool premultiplyAlpha = true)
    {
        return InternalLoad(image, firstCode, fallbackCode, spacing, scale, offset, mipLevelsCount, premultiplyAlpha,
            true);
    }

    public static BitmapFont Load(Stream stream, char firstCode, char fallbackCode, Vector2 spacing, float scale,
        Vector2 offset, int mipLevelsCount = 1, bool premultiplyAlpha = true)
    {
        return Load(Image.Load(stream), firstCode, fallbackCode, spacing, scale, offset, mipLevelsCount,
            premultiplyAlpha);
    }

    public static BitmapFont Load(string fileName, char firstCode, char fallbackCode, Vector2 spacing, float scale,
        Vector2 offset, int mipLevelsCount = 1, bool premultiplyAlpha = true)
    {
        using (var stream = Storage.OpenFile(fileName, OpenFileMode.Read))
        {
            return Load(stream, firstCode, fallbackCode, spacing, scale, offset, mipLevelsCount, premultiplyAlpha);
        }
    }

    internal static BitmapFont InternalLoad(Image image, char firstCode, char fallbackCode, Vector2 spacing,
        float scale, Vector2 offset, int mipLevelsCount, bool premultiplyAlpha, bool createTexture)
    {
        var list = new List<Rectangle>(FindGlyphs(image));
        var list2 = new List<Rectangle>(list.Select(r => CropGlyph(image, r)));
        if (list.Count == 0)
        {
            throw new InvalidOperationException("No glyphs found in BitmapFont image.");
        }

        var num = int.MaxValue;
        var num2 = int.MaxValue;
        var num3 = int.MaxValue;
        var num4 = int.MaxValue;
        for (var i = 0; i < list2.Count; i++)
        {
            if (list2[i].Width > 0 && list2[i].Height > 0)
            {
                num = Math.Min(num, list2[i].Left - list[i].Left);
                num2 = Math.Min(num2, list2[i].Top - list[i].Top);
                num3 = Math.Min(num3, list[i].Right - list2[i].Right);
                num4 = Math.Min(num4, list[i].Bottom - list2[i].Bottom);
            }
        }

        int num5 = firstCode;
        var num6 = 0f;
        var list3 = new List<Glyph>();
        for (var j = 0; j < list2.Count; j++)
        {
            Vector2 texCoord;
            Vector2 texCoord2;
            Vector2 offset2;
            if (list2[j].Width > 0 && list2[j].Height > 0)
            {
                texCoord = new Vector2((list2[j].Left - 0.5f) / image.Width, (list2[j].Top - 0.5f) / image.Height);
                texCoord2 = new Vector2((list2[j].Right + 0.5f) / image.Width, (list2[j].Bottom + 0.5f) / image.Height);
                offset2 = new Vector2(list2[j].Left - list[j].Left - num - 0.5f,
                    list2[j].Top - list[j].Top - num2 - 0.5f);
            }
            else
            {
                texCoord = Vector2.Zero;
                texCoord2 = Vector2.Zero;
                offset2 = Vector2.Zero;
            }

            offset2 += offset;
            float width = list[j].Width - num - num3;
            num6 = MathUtils.Max(num6, list[j].Height - num2 - num4);
            list3.Add(new Glyph((char)num5, texCoord, texCoord2, offset2, width));
            num5++;
        }

        var image2 = new Image(image.Width, image.Height);
        for (var k = 0; k < image.Pixels.Length; k++)
        {
            image2.Pixels[k] = image.Pixels[k] == Color.Magenta ? Color.Transparent : image.Pixels[k];
        }

        if (premultiplyAlpha)
        {
            Image.PremultiplyAlpha(image2);
        }

        var texture = createTexture ? Texture2D.Load(image2, mipLevelsCount) : null;
        var image3 = createTexture ? null : image2;
        var bitmapFont = new BitmapFont();
        bitmapFont.Initialize(texture, image3, list3, fallbackCode, num6, spacing, scale);
        return bitmapFont;
    }

    internal void Initialize(Texture2D? texture, Image? inputImage, IEnumerable<Glyph> glyphs, char fallbackCode,
        float glyphHeight, Vector2 spacing, float scale)
    {
        Dispose();
        Texture = texture;
        image = inputImage;
        GlyphHeight = glyphHeight;
        LineHeight = glyphHeight + spacing.Y;
        Spacing = spacing;
        Scale = scale;
        var enumerable = glyphs as Glyph[] ?? glyphs.ToArray();
        FallbackGlyph = enumerable.First(g => g.Code == fallbackCode);
        MaxGlyphCode = enumerable.Max(g => g.Code);
        glyphsByCode = new Glyph[MaxGlyphCode + 1];
        for (var i = 0; i < glyphsByCode.Length; i++)
        {
            glyphsByCode[i] = FallbackGlyph;
        }

        foreach (var glyph in enumerable)
        {
            glyphsByCode[glyph.Code] = glyph;
        }
    }

    private static IEnumerable<Rectangle> FindGlyphs(Image image)
    {
        var y = 1;
        while (y < image.Height)
        {
            int num;
            for (var x = 1; x < image.Width; x = num)
            {
                if (image.GetPixel(x, y) != Color.Magenta && image.GetPixel(x - 1, y) == Color.Magenta &&
                    image.GetPixel(x, y - 1) == Color.Magenta)
                {
                    var i = 1;
                    var j = 1;
                    for (; x + i < image.Width && image.GetPixel(x + i, y) != Color.Magenta; i++)
                    {
                    }

                    for (; y + j < image.Height && image.GetPixel(x, y + j) != Color.Magenta; j++)
                    {
                    }

                    yield return new Rectangle(x, y, i, j);
                }

                num = x + 1;
            }

            num = y + 1;
            y = num;
        }
    }

    private static Rectangle CropGlyph(Image image, Rectangle rectangle)
    {
        var num = int.MaxValue;
        var num2 = int.MaxValue;
        var num3 = int.MinValue;
        var num4 = int.MinValue;
        for (var i = rectangle.Left; i < rectangle.Left + rectangle.Width; i++)
        for (var j = rectangle.Top; j < rectangle.Top + rectangle.Height; j++)
        {
            if (image.GetPixel(i, j).A != 0)
            {
                num = MathUtils.Min(num, i);
                num2 = MathUtils.Min(num2, j);
                num3 = MathUtils.Max(num3, i);
                num4 = MathUtils.Max(num4, j);
            }
        }

        return num == int.MaxValue
            ? new Rectangle(rectangle.Left, rectangle.Top, 0, 0)
            : new Rectangle(num, num2, num3 - num + 1, num4 - num2 + 1);
    }

    public class Glyph(char code, Vector2 texCoord1, Vector2 texCoord2, Vector2 offset, float width)
    {
        public readonly char Code = code;

        public readonly bool IsBlank = texCoord1 == texCoord2;

        public readonly Vector2 Offset = offset;

        public readonly Vector2 TexCoord1 = texCoord1;

        public readonly Vector2 TexCoord2 = texCoord2;

        public readonly float Width = width;
    }
}
