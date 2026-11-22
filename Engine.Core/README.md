# SCNET.Engine.Core

SCNET 的基础类型与通用运行支持，面向 .NET 10。不引用窗口、图形、音频、游戏或原生库项目。

| 目录 / 命名空间 | 职责 |
| --- | --- |
| `Mathematics/` / `Engine.Core.Mathematics` | 向量、矩阵、四元数、颜色、坐标、包围体、随机算法及数值比较 |
| `Collections/` / `Engine.Core.Collections` | 动态数组、只读列表视图及集合扩展 |
| `Logging/` / `Engine.Core.Logging` | 日志入口、级别、输出接口及日志接收器 |
| `Text/` / `Engine.Core.Text` | 基础文本格式化与 Bech32m 编解码 |
| `Security/` / `Engine.Core.Security` | 固定 SCNET 身份地址规则：32 字节指纹、`scnet` 前缀、64 字符 Bech32m |
| `Serialization/` / `Engine.Core.Serialization` | 基础类型、集合文本及数学值的可读文本转换，支持 Attribute 标注的扩展转换器 |
| `Reflection/` / `Engine.Core.Reflection` | 按完整运行时名称解析类型，不管理模板持久化短名 |
| `FileStorage/` / `Engine.Core.FileStorage` | 路径根、文件系统实现和同步文件访问，不依赖窗口或文件选择器 |
| `Runtime/` / `Engine.Core.Runtime` | 运行模式、时钟、主线程调度及状态机 |
| `Pathfinding/` / `Engine.Core.Pathfinding` | A* 搜索及可替换的世界查询与存储接口 |
| `Diagnostics/` / `Engine.Core.Diagnostics` | 滑动统计与性能采样 |

工具可直接引用 `SCNET.Engine.Core`，上层引擎与游戏 SDK 通过 NuGet 传递此依赖。

## 文本与运行时类型

`HumanReadableConverter.ConvertFromString` 在非法输入、未知类型或转换器返回不匹配值时抛出
带目标类型信息的 `InvalidOperationException` 并保留内部异常；`TryConvertFromString` 返回
false 和默认输出，不把错误输入替换成 `Guid.Empty`。合法空 Guid 仍可正常往返。
数字和数学值的文本格式保持区域无关；这里不定义 XML、MessagePack 或 JSON 的对象结构。

扩展类型继续通过 `HumanReadableConverterAttribute` 和 `IHumanReadableConverter` 提供转换器。
发现目录按程序集弱关联缓存，不缓存缺失转换器；新程序集加载后重查类型。解析缓存不保留可回收
转换器或运行时类型，避免阻止模组上下文释放。`RuntimeTypeResolver` 仅按完整类型名称查找，
`int`、`Vector3` 等实体模板格式短名由 `EntitySystem.TemplatesDatabase.TemplateTypeNames` 管理。
