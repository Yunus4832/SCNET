using Engine.Core;
using Engine.Media;

namespace Engine.Test.Media;

public class ImageTest
{
    [Fact]
    public void SaveFlushesPixelsCache()
    {
        var image = new Image(1, 1)
        {
            Pixels =
            {
                [0] = Color.Red
            }
        };

        using var stream = new MemoryStream();
        Image.Save(image, stream, ImageFileFormat.Png, true, sync: true);
        stream.Position = 0;

        var loaded = Image.Load(stream);

        Assert.Equal(Color.Red.PackedValue, loaded.GetPixel(0, 0).PackedValue);
    }
}
