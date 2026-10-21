namespace Scnet.UiPreviewTool.Test;

public class PreviewOptionsTest
{
    [Fact]
    public void XmlAndFactoryModesHaveIndependentValidation()
    {
        var xml = PreviewOptions.Parse(
            ["--xml", "sample.xml", "--output", "out", "--size", "640x480", "--scale", "1.5"]
        );
        Assert.Null(xml.FactoryType);
        Assert.Equal(640, xml.Width);
        Assert.Equal(1.5f, xml.Scale);
        var factory =
            PreviewOptions.Parse(["--assembly", "fixture.dll", "--factory", "Example.Scene", "--output", "out"]);
        Assert.Null(factory.XmlPath);
        Assert.Equal("Example.Scene", factory.FactoryType);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("0")]
    [InlineData("5")]
    public void InvalidScaleCannotReachGraphicsAllocation(string scale) =>
        Assert.Throws<ArgumentException>(() =>
            PreviewOptions.Parse(["--xml", "a.xml", "--output", "out", "--scale", scale])
        );

    [Theory]
    [InlineData("1x1")]
    [InlineData("9000x640")]
    [InlineData("-1x640")]
    [InlineData("960")]
    public void InvalidViewportCannotReachGraphicsAllocation(string size) =>
        Assert.Throws<ArgumentException>(() =>
            PreviewOptions.Parse(["--xml", "a.xml", "--output", "out", "--size", size])
        );

    [Fact]
    public void AmbiguousMissingAndDuplicateOptionsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => PreviewOptions.Parse(["--xml", "a.xml"]));
        Assert.Throws<ArgumentException>(() => PreviewOptions.Parse(["--assembly", "a.dll", "--output", "out"]));
        Assert.Throws<ArgumentException>(() =>
            PreviewOptions.Parse(["--xml", "a.xml", "--factory", "Factory", "--output", "out"])
        );
        Assert.Throws<ArgumentException>(() =>
            PreviewOptions.Parse(["--xml", "a.xml", "--xml", "b.xml", "--output", "out"])
        );
        Assert.Throws<ArgumentException>(() =>
            PreviewOptions.Parse(["--xml", "a.xml", "--culture", "../bad", "--output", "out"])
        );
    }
}
