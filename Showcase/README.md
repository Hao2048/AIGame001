# 老板来了 · 展示素材

- `boss-coming-showcase.mp4`：约 26 秒，1920×1080，30 fps，H.264 / AAC 立体声。包含游戏原声、原创程序配乐和中文字幕。
- `preview.gif`：约 6 秒的循环预览，适合放在 GitHub README。
- `cover.png`：从成片提取的封面帧。

镜头顺序：老板探头 → 带薪打枪 → 老板接电话 → 极限切屏 → 第 7 个最终版 → 贪枪被抓 → 训话 → 老板走了继续摸鱼。

所有游戏画面来自本项目的实际 Unity 渲染。为挑选笑点，录制器使用固定随机种子、编排的切屏输入和现有老板协程；展示用工作文件名与训话台词从游戏已有文案中选取，录制结束自动还原。没有改写得分、生命值或判定规则。训话近景、慢放、放大和字幕属于视频剪辑，不代表游戏新增操作视角。

## 在仓库 README 中展示

将以下 Markdown 放入仓库根目录 README：

```md
[![老板来了 · 实机演示](Showcase/preview.gif)](Showcase/boss-coming-showcase.mp4)
```

也可以将 MP4 拖入 GitHub Issue 或编辑页面的上传区域，再使用 GitHub 返回的视频链接。GIF 可直接在仓库 README 中展示，MP4 保留为完整下载版本。

## 重新录制和剪辑

1. 用 Unity 打开项目并进入 Play。
2. 选择 `Tools / 老板来了 / ④ 录制搞笑演示（Play 模式）`。原始画面、事件时间线及同步游戏音频写入 `Showcase/raw/`。
3. 运行 `python3 Showcase/edit/render_showcase.py`，需要 Python 的 Pillow、NumPy 和 FFmpeg。录制器优先读取 `FFMPEG_PATH`，否则尝试本机 Homebrew 路径或 PATH 中的 `ffmpeg`。
4. 默认中文字体使用本机 Hiragino Sans GB；其他系统可通过 `SHOWCASE_FONT` 指定中文字体文件。

录制器位于 `Assets/Scripts/Editor/ShowcaseCapture.cs`，不进入游戏构建。录制结束会恢复实时音频、录帧时钟、目标帧率、文案与相机比例。原始录制与剪辑中间文件已加入 `.gitignore`。

音乐与补充提示音由剪辑脚本合成，没有使用第三方歌曲。游戏自身程序音效来自 `AudioSynth`。

## 开源前的素材处理

视频和 GIF 可以用于项目展示。当前角色使用 Renderpeople Eric，其原始模型、贴图以及基于原模型的衍生网格不能直接随公开源码分发。开源时须排除 `Assets/Resources/Realism/Renderpeople/` 与 `ArtSource/Downloaded/Renderpeople/` 下的受限数据，并另行安排下载说明或替换可再分发模型。

完整来源说明见 `../THIRD_PARTY_NOTICES.md`；官方条款：https://renderpeople.com/general-terms-and-conditions/ 。不要将整个现有素材目录直接推送到公开仓库。
