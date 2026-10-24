# 人物与素材

## 默认就能玩

公开仓库自带 `BuiltInBoss.cs` 生成的原创简化角色。它由基础网格和独立骨骼层级组成，支持行走、转身、观察、接电话及训话。无需下载第三方人物，也不会因为缺失人物文件而出现空角色或加载错误。

办公桌、显示器、办公椅、绿植和倒角基础网格来自仓库的原创 Blender 生成脚本，导出的 FBX 已包含在 `Assets/Resources/Realism/`。不安装 Blender 也能打开游戏。`Realism` 只是沿用的资源目录名，当前渲染采用 Toon 材质。

## 可选：使用视频中的人物

展示视频使用 Renderpeople **Eric Rigged 001**。它可从作者官方免费人物页面获取，但原始网格、贴图及衍生网格不包含在本仓库中。

- 官方免费资源：https://renderpeople.com/free-3d-people/
- 官方 FBX 示例包：https://renderpeople.com/sample/free/renderpeople_free_rigged_people_FBX.zip
- 授权条款：https://renderpeople.com/general-terms-and-conditions/

请自行阅读并接受适用条款后从官方获取文件。本项目不代理下载、不重新托管模型，也不把它纳入 MIT 许可证。

### 本地导入步骤

1. 解压官方 FBX 示例包，再解压其中的 `rp_eric_rigged_001_FBX.zip`。
2. 找到 Unity 版本的 `rp_eric_rigged_001_u3d.fbx`，复制为：

   ```text
   Assets/Resources/Realism/Renderpeople/Eric.fbx
   ```

3. 将作者的贴图放到下列目录，保留原文件名：

   ```text
   Assets/Resources/Realism/Renderpeople/tex/rp_eric_rigged_001_dif.jpg
   Assets/Resources/Realism/Renderpeople/tex/rp_eric_rigged_001_norm.jpg
   Assets/Resources/Realism/Renderpeople/tex/rp_eric_rigged_001_gloss.jpg
   ```

4. 等待 Unity 导入后，选择 `Tools / 老板来了 / ⑤ 配置可选下载人物`。工具设置贴图导入参数，并生成本地使用的黑色马甲背面材质分组网格。
5. 按 Play。运行时检测到人物后会使用它；未检测到时自动使用原创简化角色。

公开场景 `OfficeGame.unity` 的编辑器预览固定采用原创角色，避免在保存和提交场景时夹带可选人物的序列化骨骼数据。下载人物的本地效果在 Play 中查看。

### 哪些文件不要提交

`.gitignore` 已排除：

- `Assets/Resources/Realism/Renderpeople/` 及其元数据；包含原 FBX、贴图和本地衍生网格。
- `ArtSource/Downloaded/`、旧人物制作实验与本机临时导入目录。
- 本地 `SampleScene.unity` 预览。它可能包含可选人物实例，公开项目使用 `OfficeGame.unity`。

如果自行创建其他带下载人物的场景或 prefab，同样需要自行管理其分发范围。官方第 2.5 条说明免费资源同样受许可约束，第 4.3(b) 条限制让第三方直接获取独立 3D 数据文件；“免费”和“非商用”不等于可把源素材上传到公开仓库。

## 展示视频与字体

`Showcase/` 的视频、GIF 和封面是本项目的渲染输出，用于展示玩法。视频里的可选人物与公开仓库的默认角色外观不同，机制相同。

游戏运行时调用系统中文字体，不包含操作系统字体文件。视频字幕在制作者本机渲染成像素；剪辑脚本可通过 `SHOWCASE_FONT` 选择自己的中文字体。

完整权利与来源说明见 [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md)。
