using System.Text.RegularExpressions;

namespace Game.Utils;

/// <summary>
/// 提供简单的标签字符串解析功能。
/// <para>
/// 支持的特性：
/// <list type="bullet">
/// <item><description>不嵌套的标签</description></item>
/// <item><description>标签必须成对出现，不支持自动闭合</description></item>
/// <item><description>标签可携带一个属性，格式为 <c>&lt;tag=value&gt;content&lt;/tag&gt;</c></description></item>
/// <item><description>使用反斜杠转义特殊字符：<c>\</c>、<c>\&lt;</c>、<c>\&gt;</c></description></item>
/// </list>
/// </para>
/// </summary>
public abstract partial class SimpleTagParser
{
    /// <summary>
    /// 解析包含标签的字符串，将其分割为文本和标签对象的列表。
    /// </summary>
    /// <param name="input">要解析的输入字符串。</param>
    /// <returns>包含 <see cref="ParsedItem"/> 对象的列表，表示解析后的文本和标签。</returns>
    public static List<ParsedItem> Parse(string input)
    {
        var results = new List<ParsedItem>();
        if (string.IsNullOrEmpty(input))
        {
            return results;
        }

        var regex = GenRegex();

        var lastIndex = 0;
        foreach (Match match in regex.Matches(input))
        {
            if (match.Index > lastIndex)
            {
                var leadingText = input.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrEmpty(leadingText))
                {
                    results.Add(ParsedItem.Parse(leadingText));
                }
            }

            if (match.Groups["tag"].Success || match.Groups["text"].Success)
            {
                results.Add(ParsedItem.Parse(match.Value));
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex >= input.Length)
        {
            return results;
        }

        var trailingText = input[lastIndex..];
        if (!string.IsNullOrEmpty(trailingText))
        {
            results.Add(ParsedItem.Parse(trailingText));
        }

        return results;
    }

    [GeneratedRegex(
        @"(?<text>(?:(?![<\\]).|\\[\\<>])+)|" +
        @"(?<tag><(?<name>\w+)(?:=(?<value>[\w-]+))?>" +
        @"(?<content>(?:(?!</\k<name>>|(?<!\\)<).|\\[\\<>])*?)" +
        @"</\k<name>>)",
        RegexOptions.Singleline)]
    private static partial Regex GenRegex();
}

/// <summary>
/// 表示解析后的单个元素，可以是普通文本或带属性的标签。
/// </summary>
public partial class ParsedItem
{
    private const string _gtEscape = "{{GtEscape}}";
    private const string _ltEscape = "{{LtEscape}}";
    private const string _escapeEscape = "{{EscapeEscapee}}";

    /// <summary>
    /// 获取一个值，该值指示此元素是否为标签。
    /// <para>如果为 <see langword="true"/>，表示这是一个标签元素；否则为普通文本。</para>
    /// </summary>
    public bool IsTag { get; private set; }

    /// <summary>
    /// 获取元素的原始内容字符串（包含转义字符）。
    /// <para>如需获取不含转义字符的内容，请使用 <see cref="Content"/> 属性。</para>
    /// </summary>
    public string ContentOrigin { get; private set; } = string.Empty;

    /// <summary>
    /// 获取元素的内容字符串，自动移除转义字符。
    /// <para>将 <c>\\</c> 替换为 <c>\</c>，<c>\&lt;</c> 替换为 <c>&lt;</c>，<c>\&gt;</c> 替换为 <c>&gt;</c>。</para>
    /// </summary>
    public string Content => ContentOrigin.Replace(@"\\", "\\").Replace(@"\<", "<").Replace(@"\>", ">");

    /// <summary>
    /// 获取标签的名称。
    /// <para>仅当 <see cref="IsTag"/> 为 <see langword="true"/> 时有效。对于普通文本元素，此值为空字符串。</para>
    /// </summary>
    public string TagName { get; private set; } = string.Empty;

    /// <summary>
    /// 获取标签的属性值。
    /// <para>仅当 <see cref="IsTag"/> 为 <see langword="true"/> 且标签包含属性时有效。例如 <c>&lt;tag=value&gt;</c> 中的 "value"。</para>
    /// <para>对于普通文本元素或无属性标签，此值为空字符串。</para>
    /// </summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>
    /// 解析单个输入字符串，返回一个 <see cref="ParsedItem"/> 作为解析结果。
    /// <para>
    /// 解析规则：
    /// <list type="bullet">
    /// <item><description>如果输入不是有效的标签格式（以 <c>&lt;</c> 开头，以 <c>&gt;</c> 结尾），则作为普通文本处理。</description></item>
    /// <item><description>如果标签格式不正确（如没有闭合标签或首尾标签名称不匹配），返回原始字符串。</description></item>
    /// <item><description>标签可以携带一个可选的属性值，格式为 <c>&lt;tag=value&gt;content&lt;/tag&gt;</c>。</description></item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="input">要解析的输入字符串。</param>
    /// <returns>一个 <see cref="ParsedItem"/> 对象，表示解析后的文本或标签元素。</returns>
    public static ParsedItem Parse(string input)
    {
        var item = new ParsedItem();
        var processedInput = EscapeReplace(input);

        var trimmedInput = processedInput.Trim();

        // 仅当输入以 < 开头，以 > 结尾时，才尝试解析为标签
        var tagRegex = GenRegex();
        if (trimmedInput.StartsWith('<') && trimmedInput.EndsWith('>'))
        {
            var match = tagRegex.Match(trimmedInput);

            if (match.Success)
            {
                var startTagName = match.Groups["name"].Value;
                var endTagName = match.Groups["end_name"].Value;

                // 检查开始标签和结束标签的名称是否相同
                if (startTagName.Equals(endTagName, StringComparison.Ordinal))
                {
                    item.IsTag = true;
                    item.TagName = startTagName;
                    item.Value = match.Groups["value"].Value;
                    item.ContentOrigin = EscapeRestore(match.Groups["content"].Value);
                    return item;
                }
            }
        }

        item.ContentOrigin = EscapeRestore(processedInput);
        return item;
    }

    /// <summary>
    /// 将此元素转换为字符串表示形式。
    /// <para>对于标签元素，返回带标签的字符串（不包含转义字符）；对于普通文本，返回内容字符串。</para>
    /// </summary>
    /// <returns>元素的字符串表示形式。</returns>
    public override string ToString()
    {
        return ToString(false);
    }

    /// <summary>
    /// 将此元素转换为字符串表示形式，可选择是否保留转义字符。
    /// </summary>
    /// <param name="origin">
    /// 如果为 <see langword="true"/>，返回原始内容（保留转义字符）；
    /// 如果为 <see langword="false"/>，返回处理后的内容（转义字符被替换）。
    /// </param>
    /// <returns>元素的字符串表示形式。</returns>
    public string ToString(bool origin)
    {
        if (!IsTag)
        {
            return origin ? ContentOrigin : Content;
        }

        if (string.IsNullOrEmpty(Value))
        {
            return origin
                ? $"<{TagName}>{ContentOrigin}</{TagName}>"
                : $"<{TagName}>{Content}</{TagName}>";
        }

        return origin
            ? $"<{TagName}={Value}>{ContentOrigin}</{TagName}>"
            : $"<{TagName}={Value}>{Content}</{TagName}>";
    }

    [GeneratedRegex(
        @"^<(?'name'\w+)(?:=(?'value'[\w-]+))?>\s*(?'content'.*?)\s*<\/(?'end_name'\w+)>$",
        RegexOptions.Singleline)]
    private static partial Regex GenRegex();

    private static string EscapeReplace(string input)
    {
        return input.Replace(@"\\", _escapeEscape)
            .Replace(@"\<", _ltEscape)
            .Replace(@"\>", _gtEscape);
    }

    private static string EscapeRestore(string input)
    {
        return input.Replace(_gtEscape, @"\>")
            .Replace(_ltEscape, @"\<")
            .Replace(_escapeEscape, @"\\");
    }
}
