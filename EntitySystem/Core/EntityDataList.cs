using System.Xml.Linq;
using Engine.Core;
using EntitySystem.TemplatesDatabase;
using EntitySystem.XmlUtilities;

namespace EntitySystem.Core;

public class EntityDataList
{
    public List<EntityData> EntitiesData = [];

    public EntityDataList()
    {
    }

    public EntityDataList(GameDatabase gameDatabase, XElement entitiesNode, bool ignoreInvalidEntities)
    {
        EntitiesData = new List<EntityData>(entitiesNode.Elements().Count());
        foreach (var item in entitiesNode.Elements())
        {
            try
            {
                EntitiesData.Add(new EntityData(gameDatabase, item));
            }
            catch (Exception ex)
            {
                if (!ignoreInvalidEntities)
                {
                    throw;
                }

                Log.Warning("Ignoring invalid entity. Reason: {0}", ex.Message);
            }
        }
    }

    public EntityDataList(GameDatabase gameDatabase, ValuesDictionary valuesDictionary, bool ignoreInvalidEntities)
    {
        EntitiesData = new List<EntityData>(valuesDictionary.Values.Count());
        foreach (ValuesDictionary item in valuesDictionary.Values)
        {
            try
            {
                EntitiesData.Add(new EntityData(gameDatabase, item));
            }
            catch (Exception ex)
            {
                if (!ignoreInvalidEntities)
                {
                    throw;
                }

                Log.Warning("Ignoring invalid entity. Reason: {0}", ex.Message);
            }
        }
    }

    public void Save(XElement entitiesNode)
    {
        foreach (var entitiesDatum in EntitiesData)
        {
            var entityNode = XmlUtils.AddElement(entitiesNode, "Entity");
            entitiesDatum.Save(entityNode);
        }
    }

    public void Save(ValuesDictionary valuesDictionary)
    {
        var i = 1;
        foreach (var entitiesDatum in EntitiesData)
        {
            var dict = new ValuesDictionary();
            valuesDictionary.SetValue(i.ToString(), dict);
            entitiesDatum.Save(dict);
            i++;
        }
    }
}
