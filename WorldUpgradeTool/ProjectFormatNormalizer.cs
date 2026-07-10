using System.Text.Json.Nodes;
using System.Xml.Linq;

using EntitySystem.XmlUtilities;

namespace WorldUpgradeTool;

internal static class ProjectFormatNormalizer
{
    public static void EnsureProjectXml(string directoryName)
    {
        var xmlPath = Storage.CombinePaths(directoryName, "Project.xml");
        if (Storage.FileExists(xmlPath))
        {
            return;
        }

        var jsonPath = Storage.CombinePaths(directoryName, "Project.json");
        if (!Storage.FileExists(jsonPath))
        {
            return;
        }

        string jsonText;
        using (var stream = Storage.OpenFile(jsonPath, OpenFileMode.Read))
        using (var reader = new StreamReader(stream))
        {
            jsonText = reader.ReadToEnd();
        }

        var rootObject = JsonNode.Parse(jsonText) as JsonObject ??
                         throw new InvalidOperationException("Project.json root must be a JSON object.");
        var projectNode = ConvertProjectJson(rootObject);
        using (var output = Storage.OpenFile(xmlPath, OpenFileMode.Create))
        {
            XmlUtils.SaveXmlToStream(projectNode, output, null, true);
        }

        Storage.DeleteFile(jsonPath);
        Console.WriteLine("Converted legacy Project.json to Project.xml.");
    }

    private static XElement ConvertProjectJson(JsonObject rootObject)
    {
        var projectNode = new XElement("Project");
        SetAttributeFromJsonValue(projectNode, rootObject, "Guid");
        SetAttributeFromJsonValue(projectNode, rootObject, "Name");
        SetAttributeFromJsonValue(projectNode, rootObject, "Version");

        if (rootObject["Subsystems"] is JsonObject subsystems)
        {
            var subsystemsNode = new XElement("Subsystems");
            foreach (var (name, value) in subsystems)
            {
                if (value is JsonObject childObject)
                {
                    subsystemsNode.Add(ConvertValuesObject(name, childObject));
                }
            }

            projectNode.Add(subsystemsNode);
        }

        if (rootObject["Entities"] is JsonObject entities)
        {
            var entitiesNode = new XElement("Entities");
            foreach (var (_, value) in entities)
            {
                if (value is JsonObject entityObject)
                {
                    entitiesNode.Add(ConvertEntityObject(entityObject));
                }
            }

            projectNode.Add(entitiesNode);
        }

        return projectNode;
    }

    private static XElement ConvertEntityObject(JsonObject entityObject)
    {
        var entityNode = new XElement("Entity");
        SetAttributeFromJsonValue(entityNode, entityObject, "Id");
        SetAttributeFromJsonValue(entityNode, entityObject, "Guid");
        SetAttributeFromJsonValue(entityNode, entityObject, "Name");

        if (entityObject["Overrides"] is JsonObject overrides)
        {
            AddValuesChildren(entityNode, overrides);
        }
        else
        {
            foreach (var (name, value) in entityObject)
            {
                if (name is "Id" or "Guid" or "Name")
                {
                    continue;
                }

                AddConvertedChild(entityNode, name, value);
            }
        }

        return entityNode;
    }

    private static XElement ConvertValuesObject(string name, JsonObject valueObject)
    {
        var valuesNode = new XElement("Values");
        XmlUtils.SetAttributeValue(valuesNode, "Name", name);
        AddValuesChildren(valuesNode, valueObject);
        return valuesNode;
    }

    private static void AddValuesChildren(XElement parentNode, JsonObject valueObject)
    {
        foreach (var (name, value) in valueObject)
        {
            AddConvertedChild(parentNode, name, value);
        }
    }

    private static void AddConvertedChild(XElement parentNode, string name, JsonNode? value)
    {
        switch (value)
        {
            case JsonObject childObject:
                parentNode.Add(ConvertValuesObject(name, childObject));
                break;
            case JsonArray { Count: >= 2 } array:
                parentNode.Add(ConvertValueArray(name, array));
                break;
        }
    }

    private static XElement ConvertValueArray(string name, JsonArray valueArray)
    {
        var valueNode = new XElement("Value");
        XmlUtils.SetAttributeValue(valueNode, "Name", name);
        XmlUtils.SetAttributeValue(valueNode, "Type", GetScalarText(valueArray[0]) ?? "string");
        XmlUtils.SetAttributeValue(valueNode, "Value", GetScalarText(valueArray[1]) ?? string.Empty);
        return valueNode;
    }

    private static void SetAttributeFromJsonValue(XElement node, JsonObject source, string name)
    {
        if (!source.TryGetPropertyValue(name, out var value))
        {
            return;
        }

        var text = value is JsonArray { Count: >= 2 } array ? GetScalarText(array[1]) : GetScalarText(value);
        if (!string.IsNullOrEmpty(text))
        {
            XmlUtils.SetAttributeValue(node, name, text);
        }
    }

    private static string? GetScalarText(JsonNode? node)
    {
        if (node == null)
        {
            return null;
        }

        return node.GetValueKind() switch
        {
            System.Text.Json.JsonValueKind.String => node.GetValue<string>(),
            System.Text.Json.JsonValueKind.True => "True",
            System.Text.Json.JsonValueKind.False => "False",
            _ => node.ToJsonString().Trim('"')
        };
    }
}
