# Plana Desktop 开发说明

本文面向需要构建、调试或扩展 Plana Desktop 的开发者。普通使用方法请阅读项目根目录的 [README](README.md)。

## 当前生产架构

- `src/Plana.Core`：设置、动作、Action Pack、角色包和插件协议等共享领域逻辑。
- `src/Plana.ControlCenter`：基于 WinUI 3 的设置、动作、动作组和扩展管理界面。
- `src/Plana.Companion.Native`：.NET 10 Host，负责托盘、设置监听、插件、快捷界面和 Renderer 生命周期。
- `src/Plana.Companion.Godot.Renderer`：唯一生产角色 Renderer，负责透明 Spine 渲染、动画、拖动和指针事件。
- `src/Plana.TransientUI`：快捷搜索和桌宠输入等 Windows 透明临时界面。
- `src/Plana.PluginHost`：隔离运行可执行插件的宿主进程。
- `tests`：Core 与 Transient UI 的自动化测试。
- `archive`：历史 WPF/WebView 实现和已完成 Proof，不参与生产构建或运行。

生产基线是 WinUI Control Center + .NET Host + Godot Renderer。Host 不再包含历史 Renderer fallback；发布目录缺少 Godot 文件时会明确失败。

## 环境要求

- Windows 10 或 Windows 11
- .NET 10 SDK 与 Desktop Runtime
- Windows App SDK/WinUI 3 构建环境
- 仓库 `artifacts/proof-toolchain` 下的 Godot 4.6.1 与对应 spine-godot 4.2 扩展（完整发布需要）

某些 Adobe 安装会定义机器级 `TargetPath`，仓库构建脚本会只在子构建进程中移除它，避免覆盖 MSBuild 输出路径。

## 构建与测试

普通 Release 构建：

```powershell
.\build.ps1
```

生成完整 Host、Godot Renderer、Plugin Host、示例插件和 Control Center MSIX：

```powershell
.\build.ps1 -Publish
```

完整桌宠从这里启动：

```text
artifacts/native-win-x64/Plana.Desktop.exe
```

不要直接运行 `src/Plana.Companion.Native/bin` 下的 Host。普通构建目录不包含完整 Godot 发布结构，当前版本会明确提示重新发布。

## 扩展开发

- Action Pack：参见 [docs/action-packs.md](docs/action-packs.md)。
- 可执行插件：参见 [docs/plugin-system.md](docs/plugin-system.md)。
- 角色包：参见 [docs/character-packs.md](docs/character-packs.md)。

示例插件位于 `examples/Plana.ExamplePlugin`。完整发布后，可导入 `artifacts/native-win-x64/SamplePlugins/hello` 进行验证。

Plugin Protocol v2 支持 Action、Companion Tool、Context Command 和 Content Provider。插件贡献动作时可以提供 `description`；Tool 和上下文命令引用同一次贡献中的 Action。受控 Host 能力包括 `character.activate`、`companion.content.showImage` 和 `companion.content.restore`。插件启停由设置文件热加载触发，Host 会重新协调 Plugin Host 生命周期并重建全部贡献。

## 交付约束

- `Plana.Core` 保持与 WPF、WinUI、WinForms 和 Godot 无关。
- 插件代码不得载入桌面 Host 进程，必须通过版本化协议和能力代理运行。
- 角色包和 Action Pack 是声明式数据，不包含宿主内可执行代码。
- 发布前至少运行 `build.ps1`，并验证完整发布 Host 能拉起 Godot 子进程。
