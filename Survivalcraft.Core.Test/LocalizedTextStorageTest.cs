using EntitySystem.TemplatesDatabase;

using Game.Localization;
using Game.Protocol.Localization;

namespace Survivalcraft.Core.Test;

public sealed class LocalizedTextStorageTest
{
    [Fact]
    public void WidgetAddressSurvivesStorageWithoutResolvingIt()
    {
        var source = LocalizedText.ContentWidget("VitalStatsWidget", "Choked", "Choked");
        Assert.Equal(source, LocalizedTextStorage.Load(LocalizedTextStorage.Save(source)));
    }

    [Fact]
    public void XmlStoragePreservesTreeWithoutCommaOrSemicolonEscaping()
    {
        var source = LocalizedText.Format(
            new LocalizedText("Health", "Death", "{0}"),
            LocalizedText.Database("DisplayName", "Wolf", "狼"),
            LocalizedText.Literal("a,b;c")
        );
        var saved = LocalizedTextStorage.Save(source);
        var node = new System.Xml.Linq.XElement("Text");
        saved.Save(node);
        var restored = new ValuesDictionary();
        restored.ApplyOverrides(System.Xml.Linq.XElement.Parse(node.ToString()));
        Assert.Equal(source, LocalizedTextStorage.Load(restored));
    }

    [Fact]
    public void InvalidKindAndMissingArgumentAreRejected()
    {
        var saved = LocalizedTextStorage.Save(LocalizedText.Literal("text"));
        saved.SetValue("Kind", (LocalizedTextKind)99);
        Assert.Throws<InvalidDataException>(() => LocalizedTextStorage.Load(saved));
        saved = LocalizedTextStorage.Save(
            LocalizedText.Format(LocalizedText.Literal("{0}"), LocalizedText.Literal("arg"))
        );
        saved.GetValue<ValuesDictionary>("Arguments").SetValue("invalid", new ValuesDictionary());
        Assert.ThrowsAny<Exception>(() => LocalizedTextStorage.Load(saved));
    }
}
