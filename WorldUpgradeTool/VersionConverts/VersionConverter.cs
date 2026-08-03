using System.Xml.Linq;

namespace WorldUpgradeTool.VersionConverts;

public abstract class VersionConverter
{
    public abstract string SourceVersion { get; }

    public abstract string TargetVersion { get; }

    public abstract void ConvertProjectXml(XElement projectNode);

    public abstract void ConvertWorld(string directoryName);
}
