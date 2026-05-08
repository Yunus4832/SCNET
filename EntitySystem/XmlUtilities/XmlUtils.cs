using System.Text;
using System.Xml;
using System.Xml.Linq;
using Engine.Serialization;

namespace EntitySystem.XmlUtilities;

public static class XmlUtils
{
    public static object GetAttributeValue(XElement node, string attributeName, Type type)
    {
        var attributeValue = GetAttributeValue(node, attributeName, type, false);
        if (attributeValue != null)
        {
            return attributeValue;
        }

        throw new Exception($"Required XML attribute \"{attributeName}\" not found in node \"{node.Name}\".");
    }

    public static object GetAttributeValue(XElement node, string attributeName, Type type, object defaultValue)
    {
        var xAttribute = node.Attribute(attributeName);
        if (xAttribute == null)
        {
            return defaultValue;
        }

        try
        {
            return HumanReadableConverter.ConvertFromString(type, xAttribute.Value);
        }
        catch (Exception)
        {
            return defaultValue;
        }
    }

    public static object? GetAttributeValue(XElement node, string attributeName, Type type, bool throwIfNotFound)
    {
        var xAttribute = node.Attribute(attributeName);
        if (xAttribute == null)
        {
            return throwIfNotFound ? throw new InvalidOperationException("Attribute not found") : null;
        }

        try
        {
            return HumanReadableConverter.ConvertFromString(type, xAttribute.Value);
        }
        catch (Exception)
        {
            if (throwIfNotFound)
            {
                throw;
            }

            return null;
        }
    }

    public static T GetAttributeValue<T>(XElement node, string attributeName)
    {
        return (T)GetAttributeValue(node, attributeName, typeof(T));
    }

    public static T GetAttributeValue<T>(XElement node, string attributeName, T defaultValue) where T: notnull
    {
        return (T)GetAttributeValue(node, attributeName, typeof(T), defaultValue);
    }

    public static T? GetAttributeValue<T>(XElement node, string attributeName, bool throwIfNotFound) where T: notnull
    {
        return (T?)GetAttributeValue(node, attributeName, typeof(T), throwIfNotFound);
    }

    public static void SetAttributeValue(XElement node, string attributeName, object value)
    {
        var value2 = HumanReadableConverter.ConvertToString(value);
        var xAttribute = node.Attribute(attributeName);
        if (xAttribute != null)
        {
            xAttribute.Value = value2;
        }
        else
        {
            node.Add(new XAttribute(attributeName, value2));
        }
    }

    public static XElement? FindChildElement(XElement node, string elementName, bool throwIfNotFound)
    {
        var xElement = node.Elements(elementName).FirstOrDefault();
        if (xElement != null)
        {
            return xElement;
        }

        if (throwIfNotFound)
        {
            throw new Exception($"Required XML element \"{elementName}\" not found in node \"{node.Name}\".");
        }

        return null;
    }

    public static XElement AddElement(XElement parentNode, string name)
    {
        var xElement = new XElement(name);
        parentNode.Add(xElement);
        return xElement;
    }

    public static XElement LoadXmlFromTextReader(TextReader textReader, bool throwOnError)
    {
        var xmlReaderSettings = new XmlReaderSettings
        {
            CheckCharacters = false,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        };
        using var reader = XmlReader.Create(textReader, xmlReaderSettings);
        return XElement.Load(reader, LoadOptions.None);
    }

    public static XElement LoadXmlFromStream(Stream stream, Encoding? encoding, bool throwOnError)
    {
        using var textReader = encoding != null
            ? new StreamReader(stream, encoding)
            : new StreamReader(stream, true);
        return LoadXmlFromTextReader(textReader, throwOnError);
    }

    public static XElement LoadXmlFromString(string data, bool throwOnError)
    {
        using var textReader = new StringReader(data);
        return LoadXmlFromTextReader(textReader, throwOnError);
    }

    public static void SaveXmlToTextWriter(XElement node, TextWriter textWriter, bool throwOnError)
    {
        var xmlWriterSettings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            Encoding = Encoding.UTF8,
            CloseOutput = true
        };
#if !ANDROID
        xmlWriterSettings.CheckCharacters = true;
#endif
        using var xmlWriter = XmlWriter.Create(textWriter, xmlWriterSettings);
        node.Save(xmlWriter);
        xmlWriter.Flush();
    }

    public static void SaveXmlToStream(XElement node, Stream stream, Encoding? encoding, bool throwOnError)
    {
        using TextWriter textWriter = encoding != null ? new StreamWriter(stream, encoding) : new StreamWriter(stream);
        SaveXmlToTextWriter(node, textWriter, throwOnError);
    }

    public static string SaveXmlToString(XElement node, bool throwOnError)
    {
        using var stringWriter = new StringWriter();
        SaveXmlToTextWriter(node, stringWriter, throwOnError);
        return stringWriter.ToString();
    }
}
