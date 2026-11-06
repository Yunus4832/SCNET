using Game.Localization;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Messaging;

[Collection(ConfigFileCollection.Name)]
public sealed class ClientLocalePublicationTest
{
    [Fact]
    public void ReplacingCatalogsKeepsClientPublicationRevisionMonotonic()
    {
        var previous = ClientLanguage.Catalog;
        try
        {
            var first = new LanguageCatalog();
            var second = new LanguageCatalog();
            first.CompleteInitialization("en-US");
            second.CompleteInitialization("zh-CN");
            Assert.Equal(first.Revision, second.Revision);
            var revision = ClientLanguage.Revision;
            ClientLanguage.Publish(first);
            Assert.Equal(revision + 1, ClientLanguage.Revision);
            ClientLanguage.Publish(second);
            Assert.Equal(revision + 2, ClientLanguage.Revision);
            Assert.Same(second, ClientLanguage.Catalog);
            Assert.Equal("zh-CN", ClientLanguage.Catalog.CurrentLanguage);
        }
        finally { ClientLanguage.Publish(previous); }
    }
}
