namespace Game.Utils;

public static class EnumUtils
{
    public static string GetEnumName(Type type, int value)
    {
        var num = GetEnumValues(type).IndexOf(value);
        return num >= 0 ? GetEnumNames(type)[num] : "<invalid enum>";
    }

    public static IList<string> GetEnumNames(Type type)
    {
        return Cache.Query(type).Names;
    }

    public static IList<int> GetEnumValues(Type type)
    {
        return Cache.Query(type).Values;
    }

    public struct NamesValues
    {
        public ReadOnlyList<string> Names;

        public ReadOnlyList<int> Values;
    }

    public static class Cache
    {
        public static Dictionary<Type, NamesValues> NamesValuesByType = new();

        public static NamesValues Query(Type type)
        {
            lock (NamesValuesByType)
            {
                NamesValues namesValues;
                if (!NamesValuesByType.TryGetValue(type, out var value))
                {
                    namesValues = default;
                    namesValues.Names = new ReadOnlyList<string>(new List<string>(Enum.GetNames(type)));
                    namesValues.Values = new ReadOnlyList<int>(new List<int>(Enum.GetValues(type).Cast<int>()));
                    value = namesValues;
                    NamesValuesByType.Add(type, value);
                }

                namesValues = value;
                return namesValues;
            }
        }
    }
}
