# macOS 安装与构建

## 下载与安装

从 [GitHub Releases](https://github.com/Hao2048/BossIsComing/releases/latest) 下载 `BossIsComing-v0.1.0-macOS-Universal.dmg`。

1. 双击打开 DMG。
2. 把 `BossIsComing.app` 拖到 `Applications`。
3. 从应用程序目录启动 **Boss Is Coming**。

支持 Apple Silicon 和 Intel Mac，最低 macOS 12。公开安装包采用内置原创角色，与公开源码的默认运行路径一致；无需 Unity Editor 或额外人物下载。

仅发布 DMG 安装包，应用 bundle 位于 DMG 内，不单独提供 `.app` 下载。

## 首次打开

这是独立游戏的早期预览版，未使用 Developer ID 分发证书，也未经过 Apple 公证。macOS 可能提示无法验证开发者。

请先确认文件来自本仓库 Release，并可用 Release 附带的 `SHA256SUMS.txt` 校验文件。若确认信任此来源，可按 macOS 的“系统设置 → 隐私与安全性 → 仍要打开”流程单独允许此应用。不要关闭系统整体的安全检查。

Apple 官方说明：https://support.apple.com/102445

## 操作

- 任意键开始。
- 空格、回车或鼠标点击：切换游戏 / 工作表格。
- Esc：暂停 / 继续。
- Command-Q：退出应用。

## 从源码构建

需要 Unity 6000.6.0f1 与 macOS Standalone 支持。发布构建从没有可选下载人物的干净检出目录执行，避免本机私有素材被打入公开安装包。

```sh
unity build /path/to/BossIsComing \
  --target StandaloneOSX \
  --execute-method MacReleaseBuilder.Build \
  --output-path /path/to/output/BossIsComing.app

bash scripts/package-macos.sh \
  /path/to/output/BossIsComing.app \
  /path/to/output/BossIsComing-v0.1.0-macOS-Universal.dmg
```

构建方法配置 Mono、`x64ARM64` 通用架构、窗口模式、产品名、应用标识和图标；`.app` 只是制作 DMG 的中间产物。源码内不包含签名私钥、开发者证书或公证凭据。
