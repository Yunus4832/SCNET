# SCNET.Engine.Core

SCNET 的基础类型与通用运行支持，面向 .NET 10。不引用窗口、图形、音频、游戏或原生库项目。

| 目录 / 命名空间 | 职责 |
| --- | --- |
| `Mathematics/` / `Engine.Core.Mathematics` | 向量、矩阵、四元数、颜色、坐标、包围体、随机算法及数值比较 |
| `Collections/` / `Engine.Core.Collections` | 动态数组、只读列表视图及集合扩展 |
| `Logging/` / `Engine.Core.Logging` | 日志入口、级别、输出接口及日志接收器 |
| `Text/` / `Engine.Core.Text` | Bech32m 编解码和校验（[BIP 350](https://bips.dev/350/)） |
| `Security/` / `Engine.Core.Security` | 固定 SCNET 身份地址规则：32 字节指纹、`scnet` 前缀、64 字符 Bech32m |
| `Runtime/` / `Engine.Core.Runtime` | 运行模式、时钟、主线程调度及通用运行辅助 |

独立公共类型各自保存于同名文件；随机数、常量扩展和运行模式枚举不与其他公共类型混放。
`Utilities` 的现有资源释放、结构体互操作等方法保留原有行为，位于 Runtime。

窗口异常事件数据 `UnhandledExceptionInfo` 和垂直同步枚举 `PresentationIntervalEnum` 属于
`Engine.Windowing`，不由 Core 管理。数学数据、序列化及无界面工具不需要为此依赖窗口层。

需要这些基础能力时可以直接引用本包；使用上层引擎或游戏 SDK 时会自动获得依赖。
游戏的全局 using 导入各职责命名空间，并显式将 `Random` 绑定到数学库的随机数类型。
本包不是可独立启动的应用。

## 通用算法与工具

`Mathematics` 包含曲线、线段和 Simplex 噪声；`Pathfinding` 提供独立 A* 算法及可替换的世界/存储接口。
`Collections` 提供通用集合，`Runtime` 包含状态机，`Diagnostics` 提供滑动统计和性能采样，`Text` 包含基础文本格式化。
这些类型不依赖游戏、图形宿主或序列化项目；游戏特定的路径请求和世界查询仍由游戏模块负责。
