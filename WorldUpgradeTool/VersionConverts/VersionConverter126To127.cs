using System.Xml.Linq;

using EntitySystem.XmlUtilities;

namespace WorldUpgradeTool.VersionConverts;

public class VersionConverter126To127 : VersionConverter
{
    public override string SourceVersion => "1.26";

    public override string TargetVersion => "1.27";

    public override void ConvertProjectXml(XElement projectNode)
    {
        XmlUtils.SetAttributeValue(projectNode, "Version", TargetVersion);
        ConvertTypesToEngine(projectNode);
    }

    public override void ConvertWorld(string directoryName)
    {
        var path = Storage.CombinePaths(directoryName, "Project.xml");
        XElement xElement;
        using (var stream = Storage.OpenFile(path, OpenFileMode.Read))
        {
            xElement = XmlUtils.LoadXmlFromStream(stream, null, true);
        }

        ConvertProjectXml(xElement);
        using (var stream2 = Storage.OpenFile(path, OpenFileMode.Create))
        {
            XmlUtils.SaveXmlToStream(xElement, stream2, null, true);
        }
    }

    public static void MigrateDataFromIsolatedStorageWithDialog()
    {
        try
        {
            if (!Storage.DirectoryExists("app:/.config/.isolated-storage"))
            {
                return;
            }

            Log.Information("1.26 data found, starting migration to 1.27.");
            var dialog = new BusyDialog("Please wait", "Migrating 1.26 data to 1.27 format...");
            DialogsManager.ShowDialog(null, dialog);
            Task.Run(delegate
            {
                string empty;
                string empty2;
                try
                {
                    var num = MigrateFolder("app:/.config/.isolated-storage", "data:");
                    empty = "Migration Successful";
                    empty2 = $"{num} file(s) were migrated from 1.26 to 1.27.";
                }
                catch (Exception ex2)
                {
                    empty = "Migration Failed";
                    empty2 = ex2.Message;
                    Log.Error("Migration to 1.27 failed, reason: {0}", ex2.Message);
                }

                DialogsManager.HideDialog(dialog);
                DialogsManager.ShowDialog(null, new MessageDialog(empty, empty2, "OK", string.Empty, delegate { }));
                Dispatcher.Dispatch(SettingsManager.LoadSettings);
            });
        }
        catch (Exception ex)
        {
            Log.Error("Failed to migrate data. Reason: {0}", ex.Message);
        }
    }

    private void ConvertTypesToEngine(XElement node)
    {
        foreach (var item in node.DescendantsAndSelf("Value"))
        {
            var xAttribute = item.Attribute("Type");
            xAttribute?.Value = xAttribute.Value switch
            {
                "Microsoft.Xna.Framework.Vector2" => "Engine.Vector2",
                "Microsoft.Xna.Framework.Vector3" => "Engine.Vector3",
                "Microsoft.Xna.Framework.Vector4" => "Engine.Vector4",
                "Microsoft.Xna.Framework.Quaternion" => "Engine.Quaternion",
                "Microsoft.Xna.Framework.Matrix" => "Engine.Matrix",
                "Microsoft.Xna.Framework.Point" => "Engine.Point2",
                "Microsoft.Xna.Framework.Color" => "Engine.Color",
                "Game.Point3" => "Engine.Point3",
                _ => xAttribute.Value
            };
        }
    }

    private static int MigrateFolder(string sourceFolderName, string targetFolderName)
    {
        var num = 0;
        Storage.CreateDirectory(targetFolderName);
        foreach (var item in Storage.ListDirectoryNames(sourceFolderName))
        {
            num += MigrateFolder(Storage.CombinePaths(sourceFolderName, item),
                Storage.CombinePaths(targetFolderName, item));
        }

        foreach (var item2 in Storage.ListFileNames(sourceFolderName))
        {
            MigrateFile(Storage.CombinePaths(sourceFolderName, item2), targetFolderName);
            num++;
        }

        Storage.DeleteDirectory(sourceFolderName);
        Log.Information("Migrated {0}", sourceFolderName);
        return num;
    }

    private static void MigrateFile(string sourceFileName, string targetFolderName)
    {
        Storage.CopyFile(sourceFileName, Storage.CombinePaths(targetFolderName, Storage.GetFileName(sourceFileName)));
        Storage.DeleteFile(sourceFileName);
        Log.Information("Migrated {0}", sourceFileName);
    }
}
