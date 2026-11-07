# 术启 VR：Quest 3 开发环境

初次配置及本次同步日期：2026-11-07。目标为 Quest 3 一体机、双手柄、Android 开发 APK。包版本以当前 manifest.json 与 packages-lock.json 为准。

## 打开与验证

使用 `D:\UNITY\2022.3.54f1c1\Editor\Unity.exe` 打开此工程。

打开 `Assets/_Project/Scenes/QuestEnvironment.unity`。场景包含一个 XR Origin、头显及左右控制器姿态跟踪、操作台、左右扳机反馈方块和状态面板。`Assets/_Project/Prefabs/QuestRig.prefab` 可作为后续场景的基础 Rig。

Unity 菜单 `ShuQi > Environment` 提供：

- **Validate Settings**：检查 XR 开关、控制器配置、渲染器资源、工具链路径和场景引用。
- **Build Quest 3 APK**：构建 `Builds/Android/ShuQiVR-Quest3-Development.apk`。
- **Configure Quest 3**：重新应用基础配置；已有验证场景会保留。

`Builds/Reports/configuration-validation.txt` 与 `android-build.txt` 记录最近一次软件检查及构建结果。`Builds/` 已加入 Git 忽略规则。

## 已配置的基础项

| 项目 | 配置 |
|---|---|
| Unity | 2022.3.54f1c1 |
| OpenXR | 1.13.2 |
| XR Plug-in Management | 4.5.4 |
| XR Interaction Toolkit | 2.6.5；XR Device Simulator 样本已导入 |
| Oculus XR Plugin | 4.4.0；已安装，Android／Windows 的 Oculus Loader 均未启用 |
| Input System | 1.11.2；Active Input Handling 为 Input System Package |
| URP | 14.0.11；Linear、4x MSAA、单通道立体渲染；HDR 与后处理关闭 |
| Android | IL2CPP、仅 ARM64、Vulkan、最低 API 29、目标 API 33 |
| 应用标识 | `com.shuqi.surgeryvr`；显示名称为“术启 VR” |
| Quest 功能 | Meta Quest Support；目标设备 Quest 3 |
| 控制器 | Oculus Touch 与 Meta Quest Touch Plus profiles |
| Windows 编辑器 | OpenXR 已配置；DX11；可在后续接入 SteamVR OpenXR runtime |
| SDK / NDK / JDK | 当前 Unity 编辑器的 `Data/PlaybackEngines/AndroidPlayer` 下的工具 |

目标 API 33 用于当前本地开发验证。准备发布到商店时，再核对当时的商店 API、签名与发布要求。

验证场景的相机和手柄使用 Unity Input System 的 TrackedPoseDriver；按键、摇杆、扳机与握键反馈使用 Unity XR Feature API。此组合支持 OpenXR。

## 用户变更后的复核结果

- 新增 XRI 2.6.5、Oculus XR Plugin 4.4.0，并将 XR 管理器从 4.4.0 更新到 4.5.4；OpenXR、Input System 与 URP 版本保持上表所列值。
- Android 与 Windows 各自仅启用了 OpenXR Loader；安装 Oculus 包没有造成同时启用两个加载器。当前 OpenXR 的 Quest 3 目标、Touch / Touch Plus profiles、IL2CPP / ARM64 和渲染设置仍保留。
- Oculus 的备用设置目前勾选 Quest 2、未勾选 Quest 3。该设置不作用于当前 OpenXR 主线；将来切换到 Oculus Loader 时，需要同步调整其目标设备。
- XR Device Simulator 已导入，但自动实例化关闭、Simulator Prefab 尚未指定，验证场景内也未放入模拟器。当前不会自动产生鼠标键盘模拟输入。
- 验证场景的 TrackedPoseDriver 使用 Input System，可接收模拟姿态；但 QuestEnvironmentProbe 目前从 Unity XR InputDevices 读取状态、按键和震动。XRI 模拟器只模拟 Input System 设备，因此现有诊断中的手柄显隐、按键反馈与震动不能直接用它验证。接入模拟器时需要统一这些输入读取，并保留真实设备的震动测试。
- 现有编辑器日志出现 XR_ERROR_RUNTIME_UNAVAILABLE，具体原因为找不到活动 OpenXR runtime。SteamVR 预览需要先完成头显到电脑的串流与 SteamVR runtime 设置。单独增加 Oculus 包或导入模拟器样本不会补齐这条链路。

本次在独立副本中完成新包组合的 Unity 编译、配置校验与 Android APK 构建，未运行 Configure 重置用户配置。复核报告保存在 `Builds/Reports/user-config-review.txt`。当前测试 APK 已更新为这次包组合；实际头显运行与 SteamVR 串流仍待设备就绪后验证。

截图中显示的 XR Hands 和 Unity OpenXR Meta 尚未安装，不能据注册表列表认定其功能已接入。

## 头显内检查

当前尚未连接可调试头显，硬件表现需要在设备就绪后检查。

1. 由使用者完成 Meta 开发者账户/组织的必要设置，在头显开启开发者模式。
2. USB 连接电脑，在头显内由使用者确认调试授权。
3. 确认 ADB 能识别设备，再通过 Unity Build And Run 或开发工具安装测试 APK。
4. 检查头部移动、双眼显示、地面高度、左右手柄位置、扳机/握键/摇杆/按键。
5. 左右扳机分别改变青色与橙色方块；左 X / 右 A 测试短震动；右 B 暂停或恢复。系统失焦时自动暂停，回到应用后按右 B 恢复。
6. 检查现实操作空间内的可达性、佩戴舒适度及设备上的帧率。

电脑未连接 XR runtime 时，Game 视图用于静态查看场景；头显与手柄状态会显示未跟踪。

## 后续 SteamVR 预览

按使用者计划，后续由 SteamVR 提供电脑端 OpenXR runtime。届时需先打通 Quest 3 到 SteamVR 的串流连接，并将 SteamVR 设为活动 OpenXR runtime，再在 Unity 中播放场景。单独安装 SteamVR 不能替代头显到电脑的串流链路。

本次没有安装或切换电脑端 runtime。这个基础场景使用 OpenXR 接口，后续预览不要求先导入整套 SteamVR Unity 插件。

## AutoHand 资源状态

`F:\学习资料\grb` 是一个 Unity 工程。其 `Assets/AutoHand` 目录约 51 MiB，包含抓取组件和示例，但引用了目录外的 Volumetric Audio、TMP 与 Bakery 资源；同时发现尚未解析的示例引用。原样迁入尚未满足依赖完整条件，因此按照“不能直接复制则先搁置”的要求，本次保留独立的基础环境。

继续迁入时应同时检查程序集、材质、控制器输入、`Hand / Grabbing / Grabbable / HandPlayer` 层及碰撞矩阵，并保留原 `.meta` GUID。届时用 AutoHand 的玩家 Rig 替换基础 Rig，场景内保持一套头显相机和一个 AudioListener。

## 备份与范围

变更前的 Assets、Packages、ProjectSettings 和 `.gitignore` 已备份到：

`D:\CGJ\outputs\quest3_environment_20261107\before_setup`

检查与构建日志保存在该目录的上一级。旧工程与 `grb` 均作为只读来源。

公开参考：

- [Unity OpenXR 1.13 项目配置](https://docs.unity.cn/Packages/com.unity.xr.openxr@1.13/manual/project-configuration.html)
- [Unity Meta Quest Support](https://docs.unity.cn/Packages/com.unity.xr.openxr@1.13/manual/features/metaquest.html)
- [Unity 2022.3 Android 环境配置](https://docs.unity3d.com/2022.3/Documentation/Manual/android-sdksetup.html)

本次复核补充参考：[XR Device Simulator 的输入要求](https://docs.unity.cn/Packages/com.unity.xr.interaction.toolkit@2.6/manual/xr-device-simulator.html)。
