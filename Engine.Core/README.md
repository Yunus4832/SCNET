# SCNET.Engine.Core

SCNET 的基础类型与通用运行支持，面向 .NET 10。不引用窗口、图形、音频、游戏或原生库项目。

| 目录 / 命名空间 | 职责 |
| --- | --- |
| `Mathematics/` / `Engine.Core.Mathematics` | 向量、矩阵、四元数、颜色、坐标、包围体、随机算法及数值比较 |
| `Collections/` / `Engine.Core.Collections` | 动态数组、只读列表视图及集合扩展 |
| `Logging/` / `Engine.Core.Logging` | 日志入口、级别、输出接口及日志接收器 |
| `Text/` / `Engine.Core.Text` | Bech32m 编解码和校验（[BIP 350](https://bips.dev/350/)） |
| `Security/` / `Engine.Core.Security` | 固定 SCNET 身份地址规则：32 字节指纹、`scnet` 前缀、64 字符 Bech32m |
| `Serialization/` / `Engine.Core.Serialization` | 基础类型、集合文本及数学值的可读文本转换，支持 Attribute 标注的扩展转换器 |
| `Reflection/` / `Engine.Core.Reflection` | 按完整运行时名称解析类型，不管理模板持久化短名 |
| `Runtime/` / `Engine.Core.Runtime` | 运行模式、时钟、主线程调度及状态机 |

独立公共类型各自保存于同名文件；随机数、常量扩展和运行模式枚举不与其他公共类型混放。

窗口异常事件数据 `UnhandledExceptionInfo` 和垂直同步枚举 `PresentationIntervalEnum` 属于
`Engine.Windowing`，不由 Core 管理。数学数据、序列化及无界面工具不需要为此依赖窗口层。

需要这些基础能力时可以直接引用本包；使用上层引擎或游戏 SDK 时会自动获得依赖。
游戏的全局 using 导入各职责命名空间，并显式将 `Random` 绑定到数学库的随机数类型。
本包不是可独立启动的应用。

## 通用算法与工具

`Mathematics` 包含曲线、线段和 Simplex 噪声；`Pathfinding` 提供独立 A* 算法及可替换的世界/存储接口。
`Collections` 提供通用集合，`Runtime` 包含状态机，`Diagnostics` 提供滑动统计和性能采样，`Text` 包含基础文本格式化。
这些类型不依赖游戏或图形宿主；游戏特定的路径请求和世界查询仍由游戏模块负责。

## 文本与运行时类型

`HumanReadableConverter.ConvertFromString` 在非法输入、未知类型或转换器返回不匹配值时抛出
带目标类型信息的 `InvalidOperationException` 并保留内部异常；`TryConvertFromString` 返回
false 和默认输出，不把错误输入替换成 `Guid.Empty`。合法空 Guid 仍可正常往返。
数字和数学值的文本格式保持区域无关；这里不定义 XML、MessagePack 或 JSON 的对象结构。

扩展类型继续通过 `HumanReadableConverterAttribute` 和 `IHumanReadableConverter` 提供转换器。
发现目录按程序集弱关联缓存，不缓存缺失转换器；新程序集加载后重查类型。解析缓存不保留可回收
转换器或运行时类型，避免阻止模组上下文释放。`RuntimeTypeResolver` 仅按完整类型名称查找，
`int`、`Vector3` 等实体模板格式短名由 `EntitySystem.TemplatesDatabase.TemplateTypeNames` 管理。
