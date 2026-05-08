using System.Collections;
using System.Xml.Linq;
using Engine.Serialization;
using EntitySystem.XmlUtilities;
using MessagePack;
using MessagePack.Resolvers;

namespace EntitySystem.TemplatesDatabase;

public class ValuesDictionary : IEnumerable<KeyValuePair<string, object>>
{
    private readonly Dictionary<string, object> _dictionary = new();

    public int Count => _dictionary.Count;

    public IEnumerable<string> Keys => _dictionary.Keys;

    public IEnumerable<object> Values => _dictionary.Values;

    public DatabaseObject DatabaseObject
    {
        get => field is not null ? field : throw new InvalidOperationException("ValueDictionary was not initialized");
        set;
    } = null!;

    public object this[string key]
    {
        get => GetValue<object>(key);
        set => SetValue(key, value);
    }

    public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
    {
        return _dictionary.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _dictionary.GetEnumerator();
    }

    public bool ContainsKey(string key)
    {
        return _dictionary.ContainsKey(key);
    }

    public T GetValue<T>(string key)
    {
        if (_dictionary.TryGetValue(key, out var value))
        {
            return (T)value;
        }

        throw new InvalidOperationException($"Required value \"{key}\" not found in values dictionary");
    }

    public T GetValue<T>(string key, T defaultValue)
    {
        if (_dictionary.TryGetValue(key, out var value))
        {
            return (T)value;
        }

        return defaultValue;
    }

    public T? GetValue<T>(string key, bool throwIfNotFound)
    {
        if (throwIfNotFound)
        {
            return GetValue<T>(key);
        }

        if (_dictionary.TryGetValue(key, out var value))
        {
            return (T?)value;
        }

        return default;
    }

    public void SetValue<T>(string key, T value) where T : notnull
    {
        _dictionary[key] = value;
    }

    public void Add<T>(string key, T value) where T : notnull
    {
        _dictionary.Add(key, value);
    }

    public void Clear()
    {
        _dictionary.Clear();
    }

    public void Save(XElement node)
    {
        foreach (var item in _dictionary)
        {
            if (item.Value is ValuesDictionary valuesDictionary)
            {
                var node2 = XmlUtils.AddElement(node, "Values");
                XmlUtils.SetAttributeValue(node2, "Name", item.Key);
                valuesDictionary.Save(node2);
            }
            else
            {
                var node3 = XmlUtils.AddElement(node, "Value");
                XmlUtils.SetAttributeValue(node3, "Name", item.Key);
                XmlUtils.SetAttributeValue(node3, "Type",
                    TypeCache.GetShortTypeName(item.Value.GetType().FullName ??
                                               throw new InvalidOperationException("Can not get type FullName")));
                XmlUtils.SetAttributeValue(node3, "Value", item.Value);
            }
        }
    }

    public void PopulateFromDatabaseObject(DatabaseObject databaseObject)
    {
        DatabaseObject = databaseObject;
        foreach (var effectiveNestingChild in databaseObject.GetEffectiveNestingChildren(null, true))
        {
            if (effectiveNestingChild.Type.SupportsValue)
            {
                if (effectiveNestingChild.Value is ProceduralValue proceduralValue)
                {
                    var value = proceduralValue.Parse(databaseObject);
                    SetValue(effectiveNestingChild.Name, value);
                }
                else
                {
                    SetValue(effectiveNestingChild.Name, effectiveNestingChild.Value);
                }
            }
            else
            {
                var valuesDictionary = new ValuesDictionary();
                valuesDictionary.PopulateFromDatabaseObject(effectiveNestingChild);
                SetValue(effectiveNestingChild.Name, valuesDictionary);
            }
        }
    }

    public void ApplyOverrides(ValuesDictionary overridesValuesDictionary)
    {
        foreach (var item in overridesValuesDictionary)
        {
            if (item.Value is ValuesDictionary valuesDictionary)
            {
                if (GetValue<object>(item.Key, false) is not ValuesDictionary valuesDictionary2)
                {
                    valuesDictionary2 = new ValuesDictionary();
                    SetValue(item.Key, valuesDictionary2);
                }

                valuesDictionary2.ApplyOverrides(valuesDictionary);
            }
            else
            {
                SetValue(item.Key, item.Value);
            }
        }
    }

    public void ApplyOverrides(XElement overridesNode)
    {
        foreach (var item in overridesNode.Elements())
        {
            if (item.Name == "Value")
            {
                var attributeValue = XmlUtils.GetAttributeValue<string>(item, "Name");
                var attributeValue2 = XmlUtils.GetAttributeValue<string>(item, "Type", false);
                Type type;
                if (attributeValue2 == null)
                {
                    var value = GetValue<object>(attributeValue, false);
                    if (value == null)
                    {
                        throw new InvalidOperationException(
                            $"Type of override \"{attributeValue}\" cannot be determined.");
                    }

                    type = value.GetType();
                }
                else
                {
                    type = TypeCache.FindType(attributeValue2, false, true)!;
                }

                var attributeValue3 = XmlUtils.GetAttributeValue(item, "Value", type);
                SetValue(attributeValue, attributeValue3);
            }
            else
            {
                if (!(item.Name == "Values"))
                {
                    throw new InvalidOperationException(
                        $"Unrecognized element \"{item.Name}\" in values dictionary overrides XML.");
                }

                var attributeValue4 = XmlUtils.GetAttributeValue<string>(item, "Name");
                if (GetValue<object>(attributeValue4, false) is not ValuesDictionary valuesDictionary)
                {
                    valuesDictionary = new ValuesDictionary();
                    SetValue(attributeValue4, valuesDictionary);
                }

                valuesDictionary.ApplyOverrides(item);
            }
        }
    }

    public void ApplyOverridesUseMessagePack(byte[] messagePackData)
    {
        var dynamicModel =
            MessagePackSerializer.Deserialize<IDictionary<object, object>>(messagePackData,
                ContractlessStandardResolver.Options);
        try
        {
            ApplyOverridesUseDictionary(dynamicModel);
        }
        catch (Exception ex)
        {
            throw new Exception("IDictionary to ValuesDictionary  error :" + ex.Message);
        }
    }

    private void ApplyOverridesUseDictionary(IDictionary<object, object> dictionary)
    {
        foreach (var keyValue in dictionary)
        {
            if (keyValue.Value is object[] objs)
            {
                var keyName = keyValue.Key.ToString();
                if (keyName is null)
                {
                    throw new InvalidOperationException("Dictionary key cannot cast to string");
                }

                var keyType = objs[0].ToString();
                Type type;
                if (keyType == null)
                {
                    var value = GetValue<object>(keyName, false);
                    if (value == null)
                    {
                        throw new InvalidOperationException($"Type of override \"{keyName}\" cannot be determined.");
                    }

                    type = value.GetType();
                }
                else
                {
                    type = TypeCache.FindType(keyType, false, true)!;
                }

                if (type == typeof(bool) || type.IsPrimitive || type == typeof(string))
                {
                    var value = Convert.ChangeType(objs[1], type);
                    SetValue(keyName, value);
                }
                else
                {
                    var theData = objs[1].ToString();
                    if (theData is null)
                    {
                        throw new InvalidOperationException("Data cannot cast to string");
                    }

                    var theValue = HumanReadableConverter.ConvertFromString(type, theData);
                    SetValue(keyName, theValue);
                }
            }
            else if (keyValue.Value is IDictionary<object, object> subDic)
            {
                var value2 = keyValue.Key.ToString();
                if (value2 is null)
                {
                    throw new InvalidOperationException("Dictionary key cannot cast to string");
                }

                if (GetValue<object>(value2, false) is not ValuesDictionary valuesDictionary)
                {
                    valuesDictionary = new ValuesDictionary();
                    SetValue(value2, valuesDictionary);
                }

                valuesDictionary.ApplyOverridesUseDictionary(subDic);
            }
        }
    }

    public string ToJsonText()
    {
        var jsonText = MessagePackSerializer.ConvertToJson(ToMessagePack());
        return jsonText;
    }

    public void ApplyOverridesUseJson(string jsonText, out byte[] data)
    {
        jsonText = jsonText.Replace("Infinity", "999999999");
        data = MessagePackSerializer.ConvertFromJson(jsonText);
        ApplyOverridesUseMessagePack(data);
    }

    public byte[] ToMessagePack()
    {
        var dic = new Dictionary<object, object>();
        SaveToDictionary(dic);
        return MessagePackSerializer.Serialize(dic);
    }

    public void SaveToDictionary(IDictionary<object, object> dictionary)
    {
        foreach (var item in _dictionary)
        {
            if (item.Value is ValuesDictionary valuesDictionary)
            {
                var subDic = new Dictionary<object, object>();
                dictionary[item.Key] = subDic;
                valuesDictionary.SaveToDictionary(subDic);
            }
            else
            {
                if (item.Value is bool || item.Value.GetType().IsPrimitive || item.Value is string)
                {
                    dictionary[item.Key] = new[]
                    {
                        TypeCache.GetShortTypeName(item.Value.GetType().FullName ??
                                                   throw new InvalidOperationException("Can not get type FullName")),
                        item.Value
                    };
                }
                else
                {
                    dictionary[item.Key] = new object[]
                    {
                        TypeCache.GetShortTypeName(item.Value.GetType().FullName ??
                                                   throw new InvalidOperationException("Can not  get type FullName")),
                        HumanReadableConverter.ConvertToString(item.Value)
                    };
                }
            }
        }
    }
}
