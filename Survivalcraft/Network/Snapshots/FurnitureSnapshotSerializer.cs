using EntitySystem.TemplatesDatabase;

namespace Game.Network.Snapshots;

public static class FurnitureSnapshotSerializer
{
    public static string SerializeDesigns(List<FurnitureDesign> chain)
    {
        var dict = new ValuesDictionary();
        for (var i = 0; i < chain.Count; i++)
        {
            var node = chain[i].Save();
            node.SetValue("NetworkIndex", chain[i].Index);
            node.SetValue("LinkedDesign", chain.IndexOf(chain[i].LinkedDesign!));
            dict.SetValue(i.ToString(System.Globalization.CultureInfo.InvariantCulture), node);
        }

        return CommonLib.SerializeVDict(dict);
    }

    public static List<FurnitureDesign> ReadDesignChain(string xml, SubsystemTerrain? terrain)
    {
        var values = CommonLib.ReadVDict(xml);
        if (values.Count > 65535)
        {
            throw new InvalidOperationException("Furniture design count exceeds capacity.");
        }

        var chain = new List<FurnitureDesign>();
        for (var i = 0; i < values.Count; i++)
        {
            var node = values.GetValue<ValuesDictionary>(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (node.GetValue<int>("Resolution") is < 2 or > FurnitureDesign.MaxResolution)
            {
                throw new InvalidOperationException("Invalid furniture resolution.");
            }

            chain.Add(new FurnitureDesign(node.GetValue<int>("NetworkIndex"), terrain, node));
        }

        foreach (var node in chain)
        {
            if (node.LoadTimeLinkedDesignIndex < -1 || node.LoadTimeLinkedDesignIndex >= chain.Count)
            {
                throw new InvalidOperationException("Invalid furniture design link index.");
            }

            if (node.LoadTimeLinkedDesignIndex >= 0)
            {
                node.LinkedDesign = chain[node.LoadTimeLinkedDesignIndex];
            }
        }

        return chain;
    }
}
