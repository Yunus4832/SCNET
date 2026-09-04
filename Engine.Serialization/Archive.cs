namespace Engine.Serialization;

public class Archive
{
    private static readonly HashSet<Assembly> _scannedAssemblies = [];

    private static readonly Dictionary<Type, SerializeData> _serializeDataByType = new();

    private static readonly Dictionary<Type, SerializeData> _pendingOptionsByType = new();

    private static readonly Dictionary<Type, TypeInfo> _genericSerializersByType = new();

    protected Archive(int version)
    {
        Version = version;
    }

    public int Version { get; set; }

    public bool UseObjectInfos { get; set; } = true;

    public static bool IsTypeSerializable(Type type)
    {
        return (ReadDelegate?)GetSerializeData(type, true).Read != null;
    }

    public static void SetTypeSerializationOptions(Type type, bool useObjectInfo, bool autoConstructObject)
    {
        lock (_serializeDataByType)
        {
            var serializeData = new SerializeData
            {
                Type = type,
                UseObjectInfo = useObjectInfo,
                AutoConstructObject = autoConstructObject
            };
            if (_serializeDataByType.TryGetValue(type, out var value))
            {
                value.MergeOptionsFrom(serializeData);
            }
            else
            {
                _pendingOptionsByType[type] = serializeData;
            }
        }
    }

    protected static SerializeData GetSerializeData(Type type, bool allowEmptySerializer)
    {
        lock (_serializeDataByType)
        {
            if (!_serializeDataByType.TryGetValue(type, out var value))
            {
                if (type.GetTypeInfo().ImplementedInterfaces.Contains(typeof(ISerializable)))
                {
                    value = CreateSerializeDataForSerializable(type);
                    AddSerializeData(value);
                }
                else
                {
                    ScanAssembliesForSerializers();
                    if (!_serializeDataByType.TryGetValue(type, out value))
                    {
                        if (type.IsArray)
                        {
                            if (_genericSerializersByType.TryGetValue(typeof(Array), out var value2))
                            {
                                value = CreateSerializeDataForSerializer(
                                    value2.MakeGenericType(type.GetElementType()!).GetTypeInfo(),
                                    type,
                                    typeof(Array)
                                );
                                if (value is null)
                                {
                                    throw new InvalidOperationException("SerializeData is null");
                                }

                                AddSerializeData(value);
                            }
                        }
                        else if (type.GetTypeInfo().IsGenericType)
                        {
                            var genericTypeDefinition = type.GetGenericTypeDefinition();
                            if (_genericSerializersByType.TryGetValue(genericTypeDefinition, out var value3))
                            {
                                value = CreateSerializeDataForSerializer(
                                    value3.MakeGenericType(type.GenericTypeArguments).GetTypeInfo(),
                                    type,
                                    type
                                );
                                if (value is null)
                                {
                                    throw new InvalidOperationException("SerializeData is null");
                                }

                                AddSerializeData(value);
                            }
                        }
                        else if (type.BaseType != null && IsTypeSerializable(type.BaseType))
                        {
                            value = GetSerializeData(type.BaseType, true).Clone();
                            value.Type = type;
                            value.AutoConstructObject = true;
                        }
                    }

                    if (value == null)
                    {
                        value = CreateEmptySerializeData(type);
                        AddSerializeData(value);
                    }
                }
            }

            if (!allowEmptySerializer && value.Read == null)
            {
                throw new InvalidOperationException(
                    $"ISerializer suitable for type \"{type.FullName}\" not found in any loaded assembly.");
            }

            return value;
        }
    }

    private static void ScanAssembliesForSerializers()
    {
        foreach (var item in TypeCache.LoadedAssemblies.Where(a => !TypeCache.IsKnownSystemAssembly(a)))
        {
            if (!_scannedAssemblies.Contains(item))
            {
                foreach (var definedType in item.DefinedTypes)
                {
                    foreach (var implementedInterface in definedType.ImplementedInterfaces)
                    {
                        if (implementedInterface.IsConstructedGenericType &&
                            implementedInterface.GetGenericTypeDefinition() == typeof(ISerializer<>))
                        {
                            if (!definedType.IsGenericType || !definedType.IsGenericTypeDefinition)
                            {
                                var type = implementedInterface.GenericTypeArguments[0];
                                if (!_serializeDataByType.ContainsKey(type))
                                {
                                    var serializeData = CreateSerializeDataForSerializer(definedType, type, type);
                                    if (serializeData != null)
                                    {
                                        AddSerializeData(serializeData);
                                    }
                                }
                            }
                            else
                            {
                                var type2 = implementedInterface.GenericTypeArguments[0];
                                var key = type2 == typeof(Array) ? type2 : type2.GetGenericTypeDefinition();
                                _genericSerializersByType.Add(key, definedType);
                            }
                        }
                    }
                }

                _scannedAssemblies.Add(item);
            }
        }
    }

    private static SerializeData CreateSerializeDataForSerializable(Type type)
    {
        return (SerializeData)typeof(Archive).GetTypeInfo()
            .GetDeclaredMethod("CreateSerializeDataForSerializableHelper")?.MakeGenericMethod(type)
            .Invoke(null, [])!;
    }

    private static SerializeData? CreateSerializeDataForSerializer(
        TypeInfo serializerType,
        Type type,
        Type parameterType
    )
    {
        var methodInfo = serializerType.GetDeclaredMethods("Serialize").FirstOrDefault(delegate (MethodInfo m)
        {
            var parameters2 = m.GetParameters();
            return parameters2.Length == 2 && parameters2[0].ParameterType == typeof(InputArchive) &&
                   parameters2[1].ParameterType == parameterType.MakeByRefType();
        });
        var methodInfo2 = serializerType.GetDeclaredMethods("Serialize").FirstOrDefault(delegate (MethodInfo m)
        {
            var parameters = m.GetParameters();
            return parameters.Length == 2 && parameters[0].ParameterType == typeof(OutputArchive) &&
                   parameters[1].ParameterType == parameterType;
        });
        if (methodInfo == null || methodInfo2 == null)
        {
            return null;
        }

        var target = Activator.CreateInstance(serializerType.AsType());
        var delegateType = typeof(ReadDelegateGeneric<>).MakeGenericType(parameterType);
        var delegateType2 = typeof(WriteDelegateGeneric<>).MakeGenericType(parameterType);
        var @delegate = methodInfo.CreateDelegate(delegateType, target);
        var delegate2 = methodInfo2.CreateDelegate(delegateType2, target);
        return (SerializeData)typeof(Archive).GetTypeInfo()
            .GetDeclaredMethod("CreateSerializeDataForSerializerHelper")!.MakeGenericMethod(type, parameterType)
            .Invoke(null, [
                @delegate,
                delegate2
            ])!;
    }

    private static SerializeData CreateSerializeDataForSerializableHelper<T>() where T : ISerializable
    {
        var serializeData = CreateEmptySerializeData(typeof(T));
        if (typeof(T).GetTypeInfo().IsValueType)
        {
            serializeData.Read = delegate (InputArchive archive, ref object? value)
            {
                var val = (T?)value;
                val?.Serialize(archive);
                value = val;
            };
        }
        else
        {
            serializeData.Read = delegate (InputArchive archive, ref object? value) { ((T?)value)?.Serialize(archive); };
        }

        serializeData.Write = delegate (OutputArchive archive, object? value) { ((T?)value)?.Serialize(archive); };
        serializeData.AutoConstructObject = true;
        return serializeData;
    }

    private static SerializeData CreateSerializeDataForSerializerHelper<T, TParam>(Delegate readDelegate,
        Delegate writeDelegate)
    {
        var readDelegateGeneric = (ReadDelegateGeneric<TParam?>)readDelegate;
        var writeDelegateGeneric = (WriteDelegateGeneric<TParam?>)writeDelegate;
        var serializeData = CreateEmptySerializeData(typeof(T));
        serializeData.Read = delegate (InputArchive archive, ref object? value)
        {
            var value2 = value != null ? (TParam)value : default;
            readDelegateGeneric(archive, ref value2);
            value = value2;
        };
        serializeData.Write = delegate (OutputArchive archive, object? value)
        {
            writeDelegateGeneric(archive, (TParam?)value);
        };
        return serializeData;
    }

    private static SerializeData CreateEmptySerializeData(Type type)
    {
        return new SerializeData
        {
            Type = type,
            UseObjectInfo = !type.GetTypeInfo().IsValueType && type != typeof(string),
            IsHumanReadableSupported = HumanReadableConverter.IsTypeSupported(type)
        };
    }

    private static void AddSerializeData(SerializeData serializeData)
    {
        if (_pendingOptionsByType.TryGetValue(serializeData.Type, out var value))
        {
            serializeData.MergeOptionsFrom(value);
        }

        _serializeDataByType.Add(serializeData.Type, serializeData);
    }

    private delegate void ReadDelegateGeneric<T>(InputArchive archive, ref T value);

    private delegate void WriteDelegateGeneric<T>(OutputArchive archive, T value);

    protected delegate void ReadDelegate(InputArchive archive, ref object? value);

    protected delegate void WriteDelegate(OutputArchive archive, object? value);

    protected class SerializeData
    {
        public bool AutoConstructObject;

        public bool IsHumanReadableSupported;

        public ReadDelegate Read = null!;

        public required Type Type;

        public bool UseObjectInfo;

        public WriteDelegate Write = null!;

        public void MergeOptionsFrom(SerializeData serializeData)
        {
            UseObjectInfo = serializeData.UseObjectInfo;
            AutoConstructObject = serializeData.AutoConstructObject;
        }

        public SerializeData Clone()
        {
            return (SerializeData)MemberwiseClone();
        }
    }
}
