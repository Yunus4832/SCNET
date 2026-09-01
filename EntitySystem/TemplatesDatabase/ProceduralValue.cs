using System.Text.RegularExpressions;

using Engine.Serialization;

namespace EntitySystem.TemplatesDatabase;

/// <summary>
///     表示一个过程值，支持通过模板语法引用数据库中的其他对象值。
///     使用 %reference% 语法引用数据库对象，支持 GUID、路径导航和继承链查找。
/// </summary>
public partial struct ProceduralValue
{
    private static readonly Regex _referenceRegex = CreateReferenceRegex();

    /// <summary>
    ///     过程值模板字符串，包含 %reference% 形式的引用。
    /// </summary>
    public string Procedure;

    /// <summary>
    ///     解析过程值模板，将其中的引用替换为实际值。
    /// </summary>
    /// <param name="context">解析上下文，作为引用查找的起始点。</param>
    /// <returns>
    ///     如果整个 Procedure 是单个引用，返回该引用对象的值（支持值类型）或名称；
    ///     否则返回替换所有引用后的字符串。
    /// </returns>
    public object Parse(DatabaseObject context)
    {
        var match = _referenceRegex.Match(Procedure);
        if (match.Success && match.Length == Procedure.Length)
        {
            var referencePath = match.Groups[1].Value;
            var targetObject = ResolveReference(context, referencePath);
            return targetObject != null
                ? targetObject.Type.SupportsValue
                    ? targetObject.Value!
                    : targetObject.Name
                : $"%'{referencePath}' not found%";
        }

        return _referenceRegex.Replace(Procedure, delegate(Match m)
        {
            var referencePath = m.Groups[1].Value;
            var targetObject = ResolveReference(context, referencePath);
            return targetObject != null
                ? targetObject.Type.SupportsValue
                    ? HumanReadableConverter.ConvertToString(targetObject.Value)
                    : targetObject.Name
                : $"%'{referencePath}' not found%";
        });
    }

    /// <summary>
    ///     解析引用路径，查找对应的数据库对象。
    /// </summary>
    /// <param name="context">查找上下文，作为搜索的起始点。</param>
    /// <param name="reference">
    ///     引用路径，支持以下格式：
    ///     <list type="bullet">
    ///         <item>GUID: 完整的 36 字符 GUID（如：12345678-1234-1234-1234-123456789abc）</item>
    ///         <item>路径: 使用 / 分隔的嵌套路径（如：Parent/Child/Property）</item>
    ///         <item>
    ///             特殊标记:
    ///             <list type="bullet">
    ///                 <item>. - 当前对象</item>
    ///                 <item>.. - 父对象</item>
    ///                 <item>... - 根对象</item>
    ///                 <item>...TypeName - 指定类型的祖先</item>
    ///                 <item>^^ - 有效继承父级</item>
    ///                 <item>^^^ - 继承根</item>
    ///                 <item>^^^TypeName - 指定类型的继承祖先</item>
    ///             </list>
    ///         </item>
    ///         <item>名称: 简单名称，向上查找链搜索</item>
    ///     </list>
    /// </param>
    /// <returns>找到的数据库对象，如果未找到则返回 null。</returns>
    public static DatabaseObject? ResolveReference(DatabaseObject? context, string reference)
    {
        // 检查是否为 GUID 格式（36字符，带4个横杠在固定位置）
        if (reference.Length == 36 &&
            reference[8] == '-' &&
            reference[13] == '-' &&
            reference[18] == '-' &&
            reference[23] == '-'
           )
        {
            var guid = new Guid(reference);
            return context?.Database?.FindDatabaseObject(guid, null, false);
        }

        // 处理包含路径分隔符或特殊导航标记的引用
        if (reference.Contains('/') || reference.Contains('.') || reference.Contains('^'))
        {
            var pathSegments = reference.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
            var segmentIndex = 0;
            while (context != null && segmentIndex < pathSegments.Length)
            {
                var segment = pathSegments[segmentIndex];
                if (segment != ".")
                {
                    if (segment == "..")
                    {
                        context = context.NestingParent;
                    }
                    else if (segment.StartsWith("..."))
                    {
                        var targetTypeName = segment[3..];
                        if (string.IsNullOrEmpty(targetTypeName))
                        {
                            context = context.NestingRoot;
                        }
                        else
                        {
                            while (context != null && context.Type.Name != targetTypeName)
                            {
                                context = context.NestingParent;
                            }
                        }
                    }
                    else if (segment == "^^")
                    {
                        context = context.EffectiveInheritanceParent;
                    }
                    else if (segment.StartsWith("^^^"))
                    {
                        var inheritanceTypeName = segment[3..];
                        if (string.IsNullOrEmpty(inheritanceTypeName))
                        {
                            context = context.EffectiveInheritanceRoot;
                        }
                        else
                        {
                            while (context != null && context.Type.Name != inheritanceTypeName)
                            {
                                context = context.EffectiveInheritanceParent;
                            }
                        }
                    }
                    else
                    {
                        // 普通名称：查找嵌套子对象并更新上下文
                        context = context.FindEffectiveNestedChild(segment, null, true, false);
                    }
                }

                segmentIndex++;
            }

            return context;
        }

        // 无路径分隔符：向上查找链搜索
        while (context != null)
        {
            var nestedChild = context.FindEffectiveNestedChild(reference, null, true, false);
            if (nestedChild != null)
            {
                return nestedChild;
            }

            context = context.NestingParent;
        }

        return null;
    }

    /// <summary>
    ///     创建用于匹配引用的正则表达式。
    ///     匹配 %reference% 格式的引用，其中 reference 可以包含字母、数字、下划线、横杠、点、斜杠和脱字符。
    /// </summary>
    /// <returns>编译后的正则表达式。</returns>
    [GeneratedRegex(@"\%([A-Za-z0-9_\-\.\/\^]+)\%", RegexOptions.Compiled)]
    private static partial Regex CreateReferenceRegex();
}
