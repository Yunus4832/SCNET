using System.Xml.Linq;
using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;

namespace EntitySystem.Core;

public class ProjectData
{
    public EntityDataList EntityDataList = new();

    public ValuesDictionary ValuesDictionary = new();

    public ProjectData()
    {
    }

    public ProjectData(GameDatabase gameDatabase, DatabaseObject projectTemplate, ValuesDictionary? overrides)
    {
        ValuesDictionary.PopulateFromDatabaseObject(projectTemplate);
        if (overrides != null)
        {
            ValuesDictionary.ApplyOverrides(overrides);
        }
    }

    public ProjectData(
        GameDatabase gameDatabase,
        byte[] msgPack,
        ValuesDictionary? overrides,
        bool ignoreInvalidEntities
    )
    {
        var rootNode = new ValuesDictionary();
        rootNode.ApplyOverridesUseMessagePack(msgPack);
        var attributeValue = rootNode.GetValue("Guid", Guid.Empty);
        var attributeValue2 = rootNode.GetValue("Name", string.Empty);
        DatabaseObject databaseObject;
        if (attributeValue != Guid.Empty)
        {
            databaseObject =
                gameDatabase.Database.FindDatabaseObject(attributeValue, gameDatabase.ProjectTemplateType, true)!;
        }
        else
        {
            if (string.IsNullOrEmpty(attributeValue2))
            {
                throw new InvalidOperationException("Project template guid or name must be specified.");
            }

            databaseObject =
                gameDatabase.Database.FindDatabaseObject(attributeValue2, gameDatabase.ProjectTemplateType, true)!;
        }

        ValuesDictionary = new ValuesDictionary();
        ValuesDictionary.PopulateFromDatabaseObject(databaseObject);
        var subsystems = rootNode.GetValue("Subsystems", new ValuesDictionary());
        if (subsystems.Count > 0)
        {
            ValuesDictionary.ApplyOverrides(subsystems);
        }

        if (overrides != null)
        {
            ValuesDictionary.ApplyOverrides(overrides);
        }

        var entities = rootNode.GetValue("Entities", new ValuesDictionary());
        if (entities.Count > 0)
        {
            EntityDataList = new EntityDataList(gameDatabase, entities, ignoreInvalidEntities);
        }
    }

    public ProjectData(
        GameDatabase gameDatabase,
        XElement projectNode,
        ValuesDictionary? overrides,
        bool ignoreInvalidEntities
    )
    {
        var attributeValue = XmlUtils.GetAttributeValue(projectNode, "Guid", Guid.Empty);
        var attributeValue2 = XmlUtils.GetAttributeValue(projectNode, "Name", string.Empty);
        DatabaseObject databaseObject;
        if (attributeValue != Guid.Empty)
        {
            databaseObject =
                gameDatabase.Database.FindDatabaseObject(attributeValue, gameDatabase.ProjectTemplateType, true)!;
        }
        else
        {
            if (string.IsNullOrEmpty(attributeValue2))
            {
                throw new InvalidOperationException("Project template guid or name must be specified.");
            }

            databaseObject =
                gameDatabase.Database.FindDatabaseObject(attributeValue2, gameDatabase.ProjectTemplateType, true)!;
        }

        ValuesDictionary = new ValuesDictionary();
        ValuesDictionary.PopulateFromDatabaseObject(databaseObject);
        var xElement = XmlUtils.FindChildElement(projectNode, "Subsystems", false);
        if (xElement != null)
        {
            ValuesDictionary.ApplyOverrides(xElement);
        }

        if (overrides != null)
        {
            ValuesDictionary.ApplyOverrides(overrides);
        }

        var xElement2 = XmlUtils.FindChildElement(projectNode, "Entities", false);
        if (xElement2 != null)
        {
            EntityDataList = new EntityDataList(gameDatabase, xElement2, ignoreInvalidEntities);
        }
    }

    public void Save(XElement projectNode)
    {
        XmlUtils.SetAttributeValue(projectNode, "Guid", ValuesDictionary.DatabaseObject.Guid);
        XmlUtils.SetAttributeValue(projectNode, "Name", ValuesDictionary.DatabaseObject.Name);
        var node = XmlUtils.AddElement(projectNode, "Subsystems");
        ValuesDictionary.Save(node);
        var entitiesNode = XmlUtils.AddElement(projectNode, "Entities");
        EntityDataList.Save(entitiesNode);
    }

    public void Save(ValuesDictionary rootNode)
    {
        var entityList = new ValuesDictionary();
        rootNode.SetValue("Guid", ValuesDictionary.DatabaseObject.Guid);
        rootNode.SetValue("Name", ValuesDictionary.DatabaseObject.Name);
        rootNode.SetValue("Subsystems", ValuesDictionary);
        rootNode.SetValue("Entities", entityList);
        EntityDataList.Save(entityList);
    }
}
