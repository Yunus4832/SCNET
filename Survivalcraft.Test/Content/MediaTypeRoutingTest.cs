using Engine.Core.Mathematics;
using Engine.Core.Runtime;
using Engine.Graphics;
using Engine.Media.Audio;
using Engine.Media.Audio.Formats;
using Engine.Media.Images;

using Game.Managers;
using Game.Modding.Content;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Content;

[Collection(ConfigFileCollection.Name)]
public sealed class MediaTypeRoutingTest
{
    [Theory]
    [InlineData(240, 120, 60, 128)]
    [InlineData(120, 60, 30, 128)]
    [InlineData(120, 60, 30, 0)]
    public void TextureReaderPreservesPreparedPixelChannels(int red, int green, int blue, int alpha)
    {
        var previousRunMode = RunMode.Value;
        RunMode.Value = RunModeType.HeadlessServer;
        ContentManager.Initialize();
        try
        {
            BuiltInContentReaders.Register();
            using var source = new Image(1, 1);
            var expected = new Color(red, green, blue, alpha);
            source.SetPixel(0, 0, expected);
            using var encoded = new MemoryStream();
            Image.Save(source, encoded, ImageFormat.Png, true);
            var content = new ContentInfo("prepared-textures/pixel.png");
            content.SetContentStream(new MemoryStream(encoded.ToArray()));
            ContentManager.Add(content);
            var texture = ContentManager.Get<Texture2D>("prepared-textures/pixel");
            var loaded = Assert.IsType<Image>(texture.Tag);
            Assert.Equal(expected, loaded.GetPixel(0, 0));
        }
        finally
        {
            ContentManager.Initialize();
            RunMode.Value = previousRunMode;
        }
    }

    [Fact]
    public void DefaultImageLookupPrefersWebPAndStillSupportsPng()
    {
        ContentManager.Initialize();
        try
        {
            BuiltInContentReaders.Register();
            foreach (var format in new[] { ImageFormat.Png, ImageFormat.WebP })
            {
                using var image = new ImageBuffer(1, 1);
                image.Pixels[0] = format == ImageFormat.WebP ? Color.Red : Color.Blue;
                var suffix = format == ImageFormat.WebP ? "webp" : "png";
                var content = new ContentInfo($"format-preference/pixel.{suffix}");
                content.SetContentStream(new MemoryStream(ImageCodec.Encode(image, format)));
                ContentManager.Add(content);
            }

            var loaded = ContentManager.Get<Image>("format-preference/pixel");
            Assert.Equal(Color.Red, loaded.Pixels[0]);
        }
        finally
        {
            ContentManager.Initialize();
        }
    }

    [Fact]
    public void ImagePayloadLoadsThroughContentReaderAndTypeConversion()
    {
        ContentManager.Initialize();
        try
        {
            BuiltInContentReaders.Register();
            using var expected = new Image(1, 1);
            var color = new Color(12, 34, 56, 255);
            expected.SetPixel(0, 0, color);
            using var encoded = new MemoryStream();
            Image.Save(expected, encoded, ImageFormat.Png, true);
            var content = new ContentInfo("media-namespaces/pixel.png");
            content.SetContentStream(new MemoryStream(encoded.ToArray()));
            ContentManager.Add(content);
            var loaded = ContentManager.Get<Image>("media-namespaces/pixel");
            Assert.Equal(color, loaded.GetPixel(0, 0));
            using var stream = new MemoryStream(encoded.ToArray());
            using var converted = Assert.IsType<Image>(ContentManager.StreamConvertType(typeof(Image), stream));
            Assert.Equal(color, converted.GetPixel(0, 0));
        }
        finally
        {
            ContentManager.Initialize();
        }
    }

    [Fact]
    public void AudioPayloadLoadsThroughContentReaderAndTypeConversion()
    {
        ContentManager.Initialize();
        try
        {
            BuiltInContentReaders.Register();
            var expected = new SoundData(1, 8000, 4);
            expected.Data[0] = 123;
            expected.Data[1] = -456;
            using var encoded = new MemoryStream();
            Wav.Save(expected, encoded);
            var content = new ContentInfo("media-namespaces/sample.wav");
            content.SetContentStream(new MemoryStream(encoded.ToArray()));
            ContentManager.Add(content);
            var loaded = ContentManager.Get<StreamingSource>("media-namespaces/sample");
            using var actual = new MemoryStream();
            loaded.CopyTo(actual);
            Assert.Equal(new byte[] { 123, 0, 56, 254 }, actual.ToArray());
            using var stream = new MemoryStream(encoded.ToArray());
            using var converted =
                Assert.IsAssignableFrom<StreamingSource>(
                    ContentManager.StreamConvertType(typeof(StreamingSource), stream)
                );
            Assert.Equal(8000, converted.SamplingFrequency);
            Assert.Equal(1, converted.ChannelsCount);
        }
        finally
        {
            ContentManager.Initialize();
        }
    }
}
