using System.Globalization;

using Engine.Core.Mathematics;
using Engine.Core.Text.Conversion;

namespace Engine.Test.Text.Conversion;

public sealed class HumanReadableConverterTest
{
    [Fact]
    public void HumanReadableVectorRoundTripsThroughDiscoveredConverter()
    {
        var expected = new Vector3(1.25f, -2.5f, 3.75f);
        var encoded = HumanReadableConverter.ConvertToString(expected);
        Assert.True(HumanReadableConverter.TryConvertFromString<Vector3>(encoded, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(typeof(int), "not-an-integer")]
    [InlineData(typeof(Guid), "not-a-guid")]
    [InlineData(typeof(Vector3), "one,two,three")]
    public void InvalidValuesThrowWithTypeContextAndTryReturnsFailure(Type type, string text)
    {
        var error = Assert.Throws<InvalidOperationException>(() => HumanReadableConverter.ConvertFromString(type, text)
        );
        Assert.Contains(type.FullName!, error.Message);
        Assert.NotNull(error.InnerException);
        Assert.False(HumanReadableConverter.TryConvertFromString(type, text, out var result));
        Assert.Null(result);
    }

    [Fact]
    public void EmptyGuidRemainsValidButMalformedGuidIsNotSilentlyAccepted()
    {
        Assert.Equal(Guid.Empty, HumanReadableConverter.ConvertFromString<Guid>(Guid.Empty.ToString()));
        Assert.False(HumanReadableConverter.TryConvertFromString<Guid>("malformed", out _));
    }

    private enum TestKind
    {
        First = 1,

        Second = 2
    }

    [Fact]
    public void TextFormatsRemainInvariantAndListsAndEnumsRoundTrip()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            object[] values =
            [
                true, 123, -1.25f, 2.5d, 3.75m, Guid.NewGuid(), new Vector3(1.25f, -2.5f, 3.75f), new Point3(1, 2, 3),
                new Color(1, 2, 3, 4), new System.Drawing.Point(4, 5), TestKind.Second
            ];
            foreach (var value in values)
            {
                var text = HumanReadableConverter.ConvertToString(value);
                Assert.Equal(value, HumanReadableConverter.ConvertFromString(value.GetType(), text));
            }

            Assert.Equal("1.25,-2.5,3.75", HumanReadableConverter.ConvertToString(new Vector3(1.25f, -2.5f, 3.75f)));
            Assert.Equal(new[] { 1, 2, 3 }, HumanReadableConverter.ValuesListFromString<int>(';', "1;2;3"));
            Assert.Throws<InvalidOperationException>(() =>
                HumanReadableConverter.ValuesListFromString<int>(';', "1;bad;3")
            );
            Assert.Empty(HumanReadableConverter.ValuesListFromString<int>(';', string.Empty));
            Assert.False(HumanReadableConverter.IsTypeSupported(typeof(Uri)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    public sealed class BrokenValue;

    [HumanReadableConverter(typeof(BrokenValue))]
    public sealed class BrokenConverter : IHumanReadableConverter
    {
        public string ConvertToString(object value) => "broken";

        public object ConvertFromString(Type type, string data) => Guid.Empty;
    }

    [Fact]
    public void ConverterReturningWrongTypeDoesNotEscapeTryAsAnInvalidCast()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            HumanReadableConverter.ConvertFromString<BrokenValue>("broken")
        );
        Assert.IsType<InvalidCastException>(error.InnerException);
        Assert.False(HumanReadableConverter.TryConvertFromString<BrokenValue>("broken", out var result));
        Assert.Null(result);
    }
}
