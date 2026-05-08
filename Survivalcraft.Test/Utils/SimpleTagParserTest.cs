using Game.Utils;

namespace Survivalcraft.Test.Utils;

/// <summary>
/// 简单标签解析噐单元测试
/// </summary>
public class SimpleTagParserTest
{
    [Fact]
    public void TestParse()
    {
        const string input = @"this is a \<Tag\> content with \> and \\ and \\<Tag=Red>real tag</Tag>";
        var items = SimpleTagParser.Parse(input);
        Assert.Equal(2, items.Count);
        Assert.Equal(@"this is a \<Tag\> content with \> and \\ and \\", items[0].ToString(true));
        Assert.Equal("<Tag=Red>real tag</Tag>",  items[1].ToString(true));
    }
}
