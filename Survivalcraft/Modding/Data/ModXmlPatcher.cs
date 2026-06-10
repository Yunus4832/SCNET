using System.Text;
using System.Xml.Linq;

using EntitySystem.XmlUtilities;

namespace Game.Modding.Data;

public static class ModXmlPatcher
{
    public static bool FindElement(XElement? xElement, Func<XElement, bool> func, out XElement? elementOut)
    {
        if (xElement is null)
        {
            elementOut = null;
            return false;
        }

        foreach (var element in xElement.Elements())
        {
            if (func(element))
            {
                elementOut = element;
                return true;
            }

            if (!FindElement(element, func, out var element1))
            {
                continue;
            }

            elementOut = element1;
            return true;
        }

        elementOut = null;
        return false;
    }

    public static bool FindElementByGuid(XElement xElement, string guid, out XElement? elementOut)
    {
        foreach (var element in xElement.Elements())
        {
            if (element.Attributes()
                .Any(xAttribute => xAttribute.Name.ToString() == "Guid" && xAttribute.Value == guid))
            {
                elementOut = element;
                return true;
            }

            if (!FindElementByGuid(element, guid, out var element1))
            {
                continue;
            }

            elementOut = element1;
            return true;
        }

        elementOut = null;
        return false;
    }

    public static bool HasAttribute(XElement element, Func<string, bool> func, out XAttribute? xAttributeOut)
    {
        foreach (var xAttribute in element.Attributes())
        {
            if (!func(xAttribute.Name.LocalName))
            {
                continue;
            }

            xAttributeOut = xAttribute;
            return true;
        }

        xAttributeOut = null;
        return false;
    }

    public static void CombineClo(XElement? xElement, Stream cloOrCr)
    {
        if (xElement is null)
        {
            return;
        }

        var mergeXml = XmlUtils.LoadXmlFromStream(cloOrCr, Encoding.UTF8, true);
        foreach (var element in mergeXml.Elements())
        {
            if (HasAttribute(element, name => name.StartsWith("new-"), out var attribute))
            {
                if (HasAttribute(element, name => name == "Index", out var xAttribute))
                {
                    if (FindElement(xElement, _ => element.Attribute("Index")!.Value == xAttribute!.Value,
                            out var element1))
                    {
                        var px = attribute!.Name.ToString()
                            .Split(["new-"], StringSplitOptions.RemoveEmptyEntries);
                        if (px.Length == 1)
                        {
                            element1!.SetAttributeValue(px[0], attribute.Value);
                        }
                    }
                }
            }
            else if (HasAttribute(element, name => name.StartsWith("r-"), out _))
            {
                if (HasAttribute(element, name => name == "Index", out var xAttribute))
                {
                    if (FindElement(xElement, _ => element.Attribute("Index")!.Value == xAttribute!.Value,
                            out var element1))
                    {
                        element1!.Remove();
                        element.Remove();
                    }
                }
            }

            xElement.Add(mergeXml);
        }
    }

    public static void CombineCr(XElement xElement, Stream cloOrCr)
    {
        var mergeXml = XmlUtils.LoadXmlFromStream(cloOrCr, Encoding.UTF8, true);
        CombineCrLogic(xElement, mergeXml);
    }

    public static void CombineCrLogic(XElement xElement, XElement needCombine)
    {
        foreach (var element in needCombine.Elements())
        {
            if (HasAttribute(element, name => name == "Result", out _))
            {
                if (HasAttribute(element, name => name.StartsWith("new-"), out var attribute))
                {
                    var px = attribute!.Name.ToString()
                        .Split(["new-"], StringSplitOptions.RemoveEmptyEntries);

                    if (FindElement(xElement, ele =>
                            {
                                foreach (var xAttribute in element.Attributes())
                                {
                                    if (xAttribute.Name == attribute.Name)
                                    {
                                        continue;
                                    }

                                    if (!HasAttribute(ele, tname => tname == xAttribute.Name, out _))
                                    {
                                        return false;
                                    }
                                }

                                return true;
                            },
                            out var element1))
                    {
                        if (px.Length == 1)
                        {
                            element1!.SetAttributeValue(px[0], attribute.Value);
                            element1.SetValue(element.Value);
                        }
                    }
                }
                else if (HasAttribute(element, name => name.StartsWith("r-"), out var attribute1))
                {
                    if (FindElement(xElement, ele =>
                        {
                            foreach (var xAttribute in element.Attributes())
                            {
                                if (xAttribute.Name == attribute1!.Name)
                                {
                                    continue;
                                }

                                if (!HasAttribute(ele, tname => tname == xAttribute.Name, out _))
                                {
                                    return false;
                                }
                            }

                            return true;
                        }, out var element1))
                    {
                        element1!.Remove();
                        element.Remove();
                    }
                }
                else
                {
                    xElement.Add(element);
                }
            }

            CombineCrLogic(xElement, element);
        }
    }

    public static void Modify(XElement source, XElement change)
    {
        if (FindElement(source, item => item.Name.LocalName == change.Name.LocalName &&
                                        item.Attribute("Guid") != null &&
                                        change.Attribute("Guid") != null &&
                                        item.Attribute("Guid")?.Value == change.Attribute("Guid")?.Value,
                out var xElement1))
        {
            foreach (var xElement in change.Elements())
            {
                Modify(xElement1!, xElement);
            }
        }
        else
        {
            if (change.Name.LocalName.StartsWith("Parameter") || change.Name.LocalName == "MemberComponentTemplate")
            {
                if (FindElement(source, item => item.Name.LocalName == change.Name.LocalName &&
                                                item.Attribute("Name")?.Name == change.Attribute("Name")?.Name,
                        out var x))
                {
                    Log.Warning($"重复的参数{x!.Name.LocalName}:{x.Attribute("Name")?.Value}设置");
                }
                else
                {
                    source.Add(change);
                }
            }
            else
            {
                source.Add(change);
            }
        }
    }

    public static void CombineDataBase(XElement? dataBaseXml, Stream xdb)
    {
        var mergeXml = XmlUtils.LoadXmlFromStream(xdb, Encoding.UTF8, true);
        var dataObjects = dataBaseXml?.Element("DatabaseObjects");
        if (dataObjects is null)
        {
            return;
        }

        foreach (var element in mergeXml.Elements())
        {
            if (HasAttribute(element, str => str.Contains("new-"), out var attribute))
            {
                if (HasAttribute(element, str => str == "Guid", out var attribute1))
                {
                    if (FindElementByGuid(dataObjects, attribute1!.Value, out var xElement))
                    {
                        var px = attribute!.Name.ToString().Split(["new-"], StringSplitOptions.RemoveEmptyEntries);
                        if (px.Length == 1)
                        {
                            xElement!.SetAttributeValue(px[0], attribute.Value);
                        }
                    }
                }
            }

            Modify(dataObjects, element);
        }
    }
}
