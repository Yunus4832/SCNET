# 模型与 glTF

模型资源使用标准 glTF 2.0：支持 `.gltf` JSON 和 `.glb` 二进制容器。游戏内置模型和示例
BeaconMod 使用 GLB；内容读取器优先选择同名 GLB。DAE 资源和 COLLADA 解析器已经移除。

## 数据与渲染分层

模型资源类型统一位于 `Engine.Media.Models`，文件位于 `Engine.Media/Models/`；
glTF 读取实现位于内部命名空间 `Engine.Media.Models.Formats`。

`Engine.Media.Models.ModelData` 是不依赖窗口、GPU 或文件存储的资源数据，不代表一种固定的渲染格式。

- `Scenes` 保存场景根节点，`DefaultSceneIndex = -1` 表示资源未指定默认场景。
- `Nodes` 保存原始矩阵或 TRS、子节点及网格、蒙皮、相机引用；保留原始 TRS，避免负缩放在
  矩阵分解后改变动画语义。读取器不会按名称删除根节点或合并节点。
- `Meshes` 保存几何体和默认形变权重；节点引用网格，允许多个节点共享同一几何体。
- `ModelMeshPartData` 对应一个 primitive，保存拓扑、材质、索引访问器、顶点属性和形变目标。
- `Accessors` 按集合索引引用，保留分量类型、归一化标记、维数和数值精度。导入时展开交错步长、
  稀疏覆盖及矩阵列对齐，得到紧密排列的小端字节；未提供底层缓冲的访问器以零初始化。
- 材质保留 metallic-roughness 参数、纹理通道、透明模式和双面属性；纹理、采样器及编码后的
  PNG/JPEG 等图像数据分别保存。读取编码数据不要求图像解码器支持该格式。
- `Skins` 保存关节节点和逆绑定矩阵；动画保存通道、采样器、插值方式及输入/输出访问器引用，
  包括 cubic-spline 的切线数据。读取不会播放动画。
- 各元素保留 `extras` 和 `extensions`，根数据保留已用/必需扩展声明。解析库不支持的必需扩展
  会使导入失败；保留扩展数据不代表渲染器已经实现其效果。

集合索引是对象身份，名称只是可重复、可省略的标签。游戏部件的名称、朝向、旋转中心和动作尺度
属于上层资源约定；通用读取器不要求 `Head`、`Body` 等固定名称。

## 外部资源与流所有权

```csharp
using Engine.Media.Models;

using var stream = OpenAsset("Models/Example.gltf");
var data = ModelData.Load(stream, relativePath => OpenAsset("Models/" + relativePath));
```

`OpenAsset` 由调用方实现。加载器不关闭主输入流，但会关闭解析回调返回的依赖流。
回调接收加载器解码后的相对资源 URI；未提供回调而资源依赖外部 `.bin` 或图像时会明确失败。
内嵌 data URI 和 GLB 的内嵌资源不需要回调。格式探测保持可定位流的位置；实际加载也支持
不可定位输入流。游戏内容读取器通过内容资源表解析相邻文件，不访问工作目录或网络。

## 当前渲染能力

`Engine.Graphics.Model` 是供刚性渲染路径使用的共享模型资源。它保留 `SourceData`，选择默认
场景（未指定时选择第一个），保存只读节点定义和共享 GPU 几何，不保存人物或控件的当前姿态。

- `ModelNode` 对应场景节点，保存 `LocalTransform`、`Parent`、`Children` 和可选 `Mesh` 引用。
  `SourceNodeIndex` 指向原始节点，`Index` 用于当前场景的运行时变换数组；它不是蒙皮关节编号。
- `RootNodes` 保留真实的多个场景根节点，不额外插入一个“根骨骼”。
- `ModelMesh` 保存几何资源，名称来自源 mesh，`SourceMeshIndex` 指向原始网格；它不保存父节点。
  `Primitives` 中的 `ModelPrimitive` 描述绘制范围、缓冲和材质索引。
- 同一源网格在相同默认形变权重下复用同一 `ModelMesh` 和缓冲；权重不同则准备独立的几何变体。
  因此 `Model.Meshes` 是唯一已准备资源的集合，可能包含同一源网格的多个权重变体。
- `MeshNodes` 是所有引用几何的节点及默认绘制顺序。渲染应遍历这些节点，不能只遍历唯一网格
  资源，否则共享同一网格的第二个节点会消失。
- 游戏按部件角色查找 `FindNode("Hand1")`，再通过 `Mesh` 或 `RequireMesh()` 获取几何。名称只
  是标签，重复标签的查找明确失败；不能把 Blender 的物体名和 mesh 数据块名混为一谈。

`ModelPose` 属于每个人物或控件，持有自身的节点动画变换、`WorldTransform` 和计算后的绝对
变换。`SetAnimationTransform` 沿用游戏刚性动画的旋转中心规则：保留默认局部平移，将动画矩阵
附加到默认旋转/缩放之后；它不代表标准 glTF 动画片段的 TRS 采样。对象放置使用 WorldTransform，
可同时作用于多个根节点。`ComponentModel` 与 `ModelWidget` 各自创建 Pose，不修改共享节点定义。

GPU 缓冲的释放责任属于 Model 所拥有的网格资源，节点和 primitive 不单独释放共享缓冲。
Model 重复 Dispose 不会重复释放资源；关联的游戏合批缓存也会在 Model 释放时清除。合批缓存按
节点绘制顺序区分条目，并为每个节点保留各自的变换索引。

`VertexDeclaration`、`VertexElement` 及其格式、语义、扩展方法由 `Engine.Graphics` 管理；
媒体库中的网格与访问器只描述资源内容。`ModelGeometryData` 在 CPU 侧准备位置、法线、UV0 的渲染布局，支持有/无索引的三角形、
triangle strip 和 triangle fan，缺失法线时生成平面法线，缺失 UV 时填零。默认形变权重会应用到
位置和法线，之后才计算几何包围盒。资产中的原始访问器和三角形绕序不因此修改。
GPU 上传根据索引范围选择 16 或 32 位索引。顶点与索引缓冲属于共享网格资源。

当前 Model/ModelWidget/游戏生物路径尚未实现通用 PBR 材质、蒙皮、动画片段播放或动态形变。
Model 读取蒙皮实例时明确抛出不支持异常；动画数据保留于 SourceData，刚性模型显示默认姿态。
现有游戏显式指定的贴图、光照和程序动画继续负责外观。不能把几何体成功上传视为材质和动画
已经正确呈现；JPEG 编码可以读取和保留，但现有图像解码器仍不解码 JPEG。

方块几何与程序生物合批仍使用 16 位索引和专用布局，超出能力时明确失败，不能将 32 位索引
直接解释为 16 位。程序生物还受现有着色器矩阵槽位限制。多材质 primitive 已保留在资源中，
但部分现有方块仅选择第一个网格部分，尚不能通用地呈现所有材质。镜像剔除和非均匀缩放的
法线变换仍需在通用材质渲染阶段补齐。

## Blender 工作流与验证

使用 Blender 自带 glTF 2.0 导出器即可，无需专用插件；推荐 GLB，保留标准 Y-up 导出约定。
物体名称成为节点名称；Mesh 数据块名称和物体名称可以不同。普通静态模型不需要游戏角色的
部件名称；套用现有程序动画时还需满足该角色的部件绑定、父级坐标系和动作单位约定。

现有弓/弩通过顶点对应关系混合不同形态，重新建模或优化时必须验证顶点对应；标准 shape key
会导出为 morph target，其数据可读取和保留，但动态权重驱动仍待渲染系统实现。

`Engine.Test/TestAssets/Models` 包含独立 Blender 导出的回归资源和生成脚本。媒体测试还覆盖
无 UV/法线、无索引、稀疏访问器、交错归一化属性、矩阵填充、32 位索引、重复名称及外部资源。
