using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;

using Game.Network.Snapshots;

namespace Survivalcraft.Core.Test.Network;

public sealed class ValuesDictionaryXmlCodecTest
{
    [Fact]
    public void RealXmlValuesRoundTripNestedDataWithoutAnyNetworkNode()
    {
        var values = new ValuesDictionary();
        values.SetValue("Name", "家具 & <snapshot>");
        values.SetValue("Count", 7);
        var nested = new ValuesDictionary();
        var identity = Guid.NewGuid();
        nested.SetValue("Identity", identity);
        values.SetValue("Nested", nested);

        var xml = ValuesDictionaryXmlCodec.Serialize(values);
        var clone = ValuesDictionaryXmlCodec.Deserialize(xml);

        Assert.Equal("Values", XElement.Parse(xml).Name.LocalName);
        Assert.Equal("家具 & <snapshot>", clone.GetValue<string>("Name"));
        Assert.Equal(7, clone.GetValue<int>("Count"));
        Assert.Equal(identity, clone.GetValue<ValuesDictionary>("Nested").GetValue<Guid>("Identity"));
        Assert.NotSame(values, clone);
        Assert.NotSame(nested, clone.GetValue<ValuesDictionary>("Nested"));
    }
}
