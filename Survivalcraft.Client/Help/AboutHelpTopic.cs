using System.Runtime.InteropServices;

using Content.Packaging;

using Engine.Graphics;

using Rendezvous.Protocol;

using ServerSource.Protocol;

namespace Game.Help;

public static class AboutHelpTopic
{
    public const string SourceUrl = "https://gitee.com/Yunus0712/scnet";

    public static string GetText()
    {
        var engineVersion = typeof(Texture2D).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+', 2)[0];
        return string.Join(
            "\n\n",
            Localization.ClientLanguage.Catalog.Get("About", "Introduction"),
            string.Join(
                '\n',
                Line("GameVersion", VersionsManager.Version),
                Line("BuildVersion", ShortBuildVersion(VersionsManager.BuildVersion)),
                Line("EngineVersion", engineVersion),
                Line(
                    "Platform",
                    $"{Platforms.ClientPlatform.Kind} / {RuntimeInformation.OSDescription} / {RuntimeInformation.ProcessArchitecture}"
                )
            ),
            string.Join(
                '\n',
                Line("GameProtocol", VersionsManager.ProtocolVersion.ToString()),
                Line("ServerSourceProtocol", ServerSourceProtocol.CurrentVersion.ToString()),
                Line("SignalingProtocol", SignalCodec.CurrentVersion.ToString()),
                Line("RelayProtocol", RelayDatagramCodec.CurrentVersion.ToString()),
                Line("PackageFormat", ContentPackageManifest.CurrentFormatVersion.ToString())
            ),
            Localization.ClientLanguage.Catalog.Get("About", "VersionExplanation"),
            Line("Source", SourceUrl)
        );
    }

    internal static string ShortBuildVersion(string version)
    {
        var parts = version.Split('+', 2);
        return parts.Length == 2 ? $"{parts[0]}+{parts[1][..Math.Min(8, parts[1].Length)]}" : version;
    }

    private static string Line(string key, string value) =>
        $"{Localization.ClientLanguage.Catalog.Get("About", key)}: {value}";
}
