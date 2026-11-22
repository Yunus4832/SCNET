using EntitySystem.TemplatesDatabase;

namespace EntitySystem.Core;

/// <summary>在解析运行时类型和构造对象之前选择项目成员。</summary>
/// <remarks>项目生命周期内须保持选择结果一致，并保留原始定义。</remarks>
public interface IProjectComposition
{
    bool ShouldCreateSubsystem(ValuesDictionary definition);

    bool ShouldCreateComponent(ValuesDictionary definition);
}
