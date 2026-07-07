using System.Xml.Linq;

using Engine.Serialization;

using EntitySystem.XmlUtilities;

using Game.TerrainSerializers;

namespace Game.VersionConverts;

public class VersionConverter128To129 : VersionConverter
{
    public override string SourceVersion => "1.28";

    public override string TargetVersion => "1.29";

    public override void ConvertProjectXml(XElement projectNode)
    {
        XmlUtils.SetAttributeValue(projectNode, "Version", TargetVersion);
        foreach (var item in from e in projectNode.Element("Subsystems")?.Elements()
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Pickables"
                 select e)
        foreach (var item2 in from e in item.Elements("Values")
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Pickables"
                 select e)
        foreach (var item3 in item2.Elements("Values"))
        foreach (var item4 in from e in item3.Elements("Value")
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Value"
                 select e)
        {
            var num = ConvertValue(XmlUtils.GetAttributeValue<int>(item4, "Value"));
            XmlUtils.SetAttributeValue(item4, "Value", num);
        }

        foreach (var item5 in from e in projectNode.Element("Subsystems")?.Elements()
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Projectiles"
                 select e)
        foreach (var item6 in from e in item5.Elements("Values")
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Projectiles"
                 select e)
        foreach (var item7 in item6.Elements("Values"))
        foreach (var item8 in from e in item7.Elements("Value")
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Value"
                 select e)
        {
            var num2 = ConvertValue(XmlUtils.GetAttributeValue<int>(item8, "Value"));
            XmlUtils.SetAttributeValue(item8, "Value", num2);
        }

        foreach (var item9 in from e in projectNode.Element("Subsystems")?.Elements()
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "CollapsingBlockBehavior"
                 select e)
        foreach (var item10 in from e in item9.Elements("Values")
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "CollapsingBlocks"
                 select e)
        foreach (var item11 in item10.Elements("Values"))
        foreach (var item12 in from e in item11.Elements("Value")
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Value"
                 select e)
        {
            var num3 = ConvertValue(XmlUtils.GetAttributeValue<int>(item12, "Value"));
            XmlUtils.SetAttributeValue(item12, "Value", num3);
        }

        foreach (var item13 in projectNode.Element("Entities")?.Elements() ?? [])
        foreach (var item14 in from e in item13.Elements("Values")
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Clothing"
                 select e)
        foreach (var item15 in from e in item14.Elements("Values")
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Clothes"
                 select e)
        foreach (var item16 in item15.Elements())
        {
            var attributeValue = XmlUtils.GetAttributeValue<string>(item16, "Value");
            var array = HumanReadableConverter.ValuesListFromString<int>(';', attributeValue);
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = ConvertValue(array[i]);
            }

            var value = HumanReadableConverter.ValuesListToString(';', array);
            XmlUtils.SetAttributeValue(item16, "Value", value);
        }

        var inventoryNames = new string[6]
        {
            "Inventory",
            "CreativeInventory",
            "CraftingTable",
            "Chest",
            "Furnace",
            "Dispenser"
        };
        foreach (var item17 in projectNode.Element("Entities")?.Elements() ?? [])
        foreach (var item18 in from e in item17.Elements("Values")
                 where inventoryNames.Contains(XmlUtils.GetAttributeValue(e, "Name", string.Empty))
                 select e)
        foreach (var item19 in from e in item18.Elements("Values")
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Slots"
                 select e)
        foreach (var item20 in item19.Elements())
        foreach (var item21 in from e in item20.Elements("Value")
                 where XmlUtils.GetAttributeValue(e, "Name", string.Empty) == "Contents"
                 select e)
        {
            var num4 = ConvertValue(XmlUtils.GetAttributeValue<int>(item21, "Value"));
            XmlUtils.SetAttributeValue(item21, "Value", num4);
        }
    }

    public override void ConvertWorld(string directoryName)
    {
        ConvertProject(directoryName);
        ConvertChunks(directoryName);
    }

    public void ConvertProject(string directoryName)
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

    public void ConvertChunks(string directoryName)
    {
        var path = Storage.CombinePaths(directoryName, "Chunks.dat");
        var path2 = Storage.CombinePaths(directoryName, "Chunks32.dat");
        using (var stream2 = Storage.OpenFile(path, OpenFileMode.Read))
        {
            using (var stream = Storage.OpenFile(path2, OpenFileMode.Create))
            {
                var array = new byte[65536];
                var array2 = new byte[131072];
                for (var i = 0; i < 65537; i++)
                {
                    TerrainSerializer129.WriteTocEntry(stream, 0, 0, -1);
                }

                var num = 0;
                while (true)
                {
                    stream2.Position = 12 * num;
                    TerrainSerializer14.ReadTocEntry(stream2, out var cx, out var cz, out var offset);
                    if (offset == 0)
                    {
                        break;
                    }

                    stream.Position = 12 * num;
                    TerrainSerializer129.WriteTocEntry(stream, cx, cz, num);
                    stream2.Position = offset;
                    stream.Position = stream.Length;
                    TerrainSerializer14.ReadChunkHeader(stream2);
                    TerrainSerializer129.WriteChunkHeader(stream, cx, cz);
                    stream2.ReadExactly(array, 0, 65536);
                    var num2 = 0;
                    for (var j = 0; j < 16; j++)
                    for (var k = 0; k < 16; k++)
                    for (var l = 0; l < 128; l++)
                    {
                        var num3 = array[2 * num2] | (array[2 * num2 + 1] << 8);
                        var num4 = ConvertValue(num3);
                        if (l < 127)
                        {
                            var num5 = num3 & 0xFF;
                            if (num5 == 18 || num5 == 92)
                            {
                                var num6 = (array[2 * num2 + 2] | (array[2 * num2 + 3] << 8)) & 0xFF;
                                if (num5 != num6)
                                {
                                    num4 |= 0x40000;
                                }
                            }
                        }

                        array2[4 * num2] = (byte)num4;
                        array2[4 * num2 + 1] = (byte)(num4 >> 8);
                        array2[4 * num2 + 2] = (byte)(num4 >> 16);
                        array2[4 * num2 + 3] = (byte)(num4 >> 24);
                        num2++;
                    }

                    stream.Write(array2, 0, 131072);
                    stream2.ReadExactly(array, 0, 1024);
                    stream.Write(array, 0, 1024);
                    num++;
                }
            }
        }

        Storage.DeleteFile(path);
    }

    public static int ConvertValue(int value)
    {
        var contents = value & 0xFF;
        var light = (value >> 8) & 0xF;
        var data = value >> 12;
        ConvertContentsLightData(ref contents, ref light, ref data);
        return contents | (light << 10) | (data << 14);
    }

    public static void ConvertContentsLightData(ref int contents, ref int light, ref int data)
    {
        if (contents >= 133 && contents <= 136)
        {
            data |= (contents - 133) << 4;
            contents = 133;
            return;
        }

        if (contents == 152)
        {
            var num = data & 1;
            var num2 = (data >> 1) & 7;
            var num3 = num == 0 ? 2 : 5;
            data = num2 | (num3 << 3);
            return;
        }

        if (contents == 182)
        {
            var num4 = data & 1;
            var num5 = (data >> 1) & 7;
            var num6 = num4 == 0 ? 3 : 0;
            data = num5 | (num6 << 3);
            return;
        }

        if (contents == 185)
        {
            var num7 = data & 3;
            var num8 = (data >> 2) & 3;
            var num9 = 0;
            if (num7 == 0)
            {
                num9 = 2;
            }

            if (num7 == 1)
            {
                num9 = 5;
            }

            if (num7 == 2)
            {
                num9 = 3;
            }

            data = num8 | (num9 << 3);
            return;
        }

        if (contents == 139)
        {
            var num10 = data & 1;
            var num11 = (data >> 1) & 7;
            var num12 = num10 * 15;
            data = num11 | (num12 << 3);
            return;
        }

        if (contents == 128)
        {
            contents = 21;
            data = (data << 1) | 1;
        }

        if (contents == 163)
        {
            contents = 3;
            data = (data << 1) | 1;
        }

        if (contents == 164)
        {
            contents = 73;
            data = (data << 1) | 1;
        }

        if (contents == 165)
        {
            contents = 67;
            data = (data << 1) | 1;
        }
    }
}
