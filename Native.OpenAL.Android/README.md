# SCNET.Native.OpenAL.Android

仓库中的 OpenAL 是已经验证可用的预编译产物，日常构建、打包和发布直接复用。
只有升级版本或明确需要修复、补充原生产物时才重新编译；替换前应完成对应 ABI 的验证，
并同步校验值和许可声明。

仅 Android 平台的 OpenAL Soft 1.25.1 原生资产包，按 `runtimes/<rid>/native/libopenal.so`
存放，支持 android-arm、android-arm64、android-x86、android-x64。

Engine 仅在 Android 目标框架引用本包，启动器无需声明文件路径或 ABI 映射。
桌面仍使用第三方 `Silk.NET.OpenAL.Soft.Native` 包。
本项目不包含图像库，不分发托管程序集，不执行 C/C++ 编译。

校验值见 `SHA256SUMS`；许可声明统一维护在根目录 `ThirdPartyNotices.txt`。
上游源码：https://github.com/kcat/openal-soft/tree/1.25.1
打包入口见 [NuGet 文档](https://gitee.com/Yunus0712/scnet/blob/main/Doc/NuGet.md#本地打包)。
