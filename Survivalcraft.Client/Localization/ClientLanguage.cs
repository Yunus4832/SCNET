using Game.Messaging;

namespace Game.Localization;

/// <summary>Owns actual client locale objects; Host callers supply their own catalog and resolver.</summary>
public static class ClientLanguage
{
    private sealed record Locale(
        LanguageCatalog Catalog,
        LocalizedTextResolver Resolver,
        GameMessageFormatter Formatter
    );

    private static Locale _locale = Create(new LanguageCatalog());

    private static int _revision;

    public static LanguageCatalog Catalog => Volatile.Read(ref _locale).Catalog;

    public static LocalizedTextResolver Resolver => Volatile.Read(ref _locale).Resolver;

    public static GameMessageFormatter Formatter => Volatile.Read(ref _locale).Formatter;

    public static int Revision => Volatile.Read(ref _revision);

    public static void Publish(LanguageCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Volatile.Write(ref _locale, Create(catalog));
        Interlocked.Increment(ref _revision);
    }

    private static Locale Create(LanguageCatalog catalog)
    {
        var resolver = new LocalizedTextResolver(catalog);
        return new Locale(catalog, resolver, new GameMessageFormatter(resolver));
    }
}
