# 开发、验证与录制

## 环境

- Unity 6000.6.0f1 / Unity 6.6。
- URP 17.6.0、Input System 1.20.0、uGUI，依赖固定在 `Packages/manifest.json` 与锁文件。
- 普通运行不需要 API Key、付费服务、Blender、FFmpeg 或 Unity CLI。
- 本地自动验证通过 Unity Pipeline 与 Unity CLI 驱动 Editor。Pipeline 为公开 Unity Registry 依赖，CLI 是可选开发工具。

## 入口与场景

打开 `Assets/Scenes/OfficeGame.unity` 后按 Play。`SlackGame.Boot()` 在场景加载后建立主控，`Awake()` 生成办公室并保存本轮组件引用。

`Tools / 老板来了 / ① 生成场景` 可刷新编辑器预览。生成前销毁旧 `GameRoot`，避免多份相机、老板或 HUD 并存。公共场景的预览始终采用原创简化角色；运行时可以检测并加载用户自行导入的可选人物。

后处理以场景根节点的 `Global Volume` / `Assets/Settings/SampleSceneProfile.asset` 为准。参数左侧的覆盖复选框必须勾选。代码只在场景完全没有 Global Volume 时创建默认配置，不在每次 Play 时覆盖 Inspector 的修改。

## 代码导览

| 文件 | 职责 |
| --- | --- |
| `SlackGame.cs` | 全局状态、输入、计时、得分、暂停与回调 |
| `BossController.cs` | 巡逻、假动作、接电话、观察、抓人、训话和离开 |
| `OfficeBuilder.cs` | 办公室、显示器、角色、相机和灯光 |
| `BuiltInBoss.cs` | 无外部素材依赖的原创简化人物 |
| `CharacterMotion.cs` | 骨骼别名、落脚点、转身跟随、接电话 IK 与脚步 |
| `ScreenDisplay.cs` | 世界空间显示器、工作表格与窗口切换 |
| `ShooterScreen.cs` | 自动枪战、敌人、弹药、击杀、受伤与复活 |
| `HudUI.cs` / `UIFactory.cs` | 进度、雷达、预警、结算、中文字体和 UI 工具 |
| `AudioSynth.cs` / `Meme.cs` | 程序音效与集中管理的梗文案 |
| `RealismLook.cs` | Toon 材质、模型加载、轮廓线、后处理与灯光配置 |
| `Editor/MoyuSceneBuilder.cs` | 生成、保存、清空场景与 Play 菜单 |
| `Editor/RealismAssetSetup.cs` | 可选人物的本地导入设置 |
| `Editor/ShowcaseCapture.cs` | 展示用实机录制，不进入游戏构建 |

游戏逻辑没有依赖下载人物的网格。老板的世界位置、观察判定与进度规则独立于角色表现层。

## 验证

在 Unity Editor 打开本项目并进入 Play 后，安装并配置可用的 Unity CLI，再运行：

```sh
python3 QA/polish_verify.py
```

该脚本会实际操作游戏，并覆盖：

- 开始游戏、切回工作。
- 暂停冻结计时与音频、继续恢复模拟。
- 基于老板实际状态切屏，完成通关并获得奖励。
- 重开重置主玩法和射击统计。
- 超时失败、忽略老板后失去三次机会、被抓序列继续推进。
- 检查游戏脚本异常。

默认以游戏操作入口调用输入，不要求 Editor 键盘焦点。`--keyboard` 可启用原生键盘注入，但依赖 Game View 焦点。

验证脚本在 `finally` 中恢复时间和音频状态。结果与开发截图保存在本地 `QA/` 或 `Assets/QA/`，不作为公开运行依赖；如出现 `Assets/QA/` 截图，请在提交前移出 Assets。

`QA/verify_volume_controls.cs` 会临时调整曝光，对比实际渲染亮度，再还原原参数，用于检查 Inspector 中的后处理是否真正生效。

## 重新生成原创家具

`ArtSource/build_office_props.py` 使用 Blender Python 生成带倒角的桌面、显示器、办公椅、绿植、键帽和杯子，并输出到资源目录。项目已经包含导出结果，重建仅用于修改美术源数据。

早期程序化 PBR 贴图脚本仍保留作参考，当前办公室 Toon 材质不依赖这些纹理文件。

## 展示视频

录制与剪辑的完整步骤在 [Showcase/README.md](../Showcase/README.md)。录制器使用现有老板协程、固定随机种子与编排输入，采集实际帧和混合音频；视频编辑包括放大、慢放、字幕和原创程序配乐。

录制结束恢复录帧时钟、实时音频、临时文案和相机比例。工具只供制作展示素材，不参与玩家运行时判定。

## 提交边界

可选人物源素材、带其骨骼的本地预览、原始录屏、录制中间文件、Library、Temp 和系统缓存均不提交。公开场景的依赖应只落到原创资源、Unity 内置资源和公开包。

做美术或规则改动后，至少重新确认通关、被抓和暂停；只有显示相关变化时，重点检查画面比例、可读性、Shader 编译和 Volume 生效情况。
