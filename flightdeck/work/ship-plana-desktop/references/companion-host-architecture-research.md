# Plana Windows 桌面伴侣宿主架构调研

> 调研日期：2026-09-01
> 范围：透明逐像素窗口、命中测试/点击穿透、拖拽、IME/文本输入、托盘、DPI、崩溃隔离、GPU 运行时与发布体积，以及现有 Spine 4.2 资源的可行性。
> 方法：仅把微软官方文档、Godot/Spine 官方文档与上游源码仓库作为结论依据；项目 issue 只用于记录已复现的边界和风险。

## 结论先行

推荐的目标架构是 **分进程：WinUI 3 控制中心/工具面板 + 专用伴侣渲染进程**。渲染进程的最终技术不要现在拍板，先做两个可丢弃的窄原型：

1. **Godot 4 + 官方 `spine-godot` GDExtension**：最快验证现有 `NP0035_spr.skel/.atlas/.png` 的画面、动作混合、透明窗口和 GPU/混合显卡表现。
2. **原生 Win32 + `spine-cpp` + Direct3D/DirectComposition（或先用 `UpdateLayeredWindow` 验证窗口语义）**：验证精确 alpha 命中、低空闲占用、拖动和 DPI；只做到一个模型、三个动作、一个交互区。

如果 Godot 原型达到透明稳定、跨应用穿透可控、空闲资源占用可接受，就优先采用 Godot 渲染子进程；否则保留分进程边界，替换为原生渲染器。**不建议把 Spine 渲染直接嵌入 WinUI 控制中心进程**：这会把 GPU/模型崩溃、窗口样式和 UI 生命周期重新绑在一起，也让未来 AI、插件和输入面板难以独立恢复。

## 已确认的平台事实

### 透明窗口与命中测试不是同一个能力

- Win32 layered window 支持逐像素 alpha。微软说明 `UpdateLayeredWindow` 适合直接提供窗口形状和内容的逐像素 alpha 场景；alpha 为零的区域可让鼠标消息穿过，而设置 `WS_EX_TRANSPARENT` 后整个 layered window 的形状会被忽略、鼠标传给其下方窗口。[Microsoft: Window Features](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features) [Microsoft: UpdateLayeredWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow)
- 仅在 `WM_NCHITTEST` 返回 `HTTRANSPARENT` 不足以实现可靠的跨应用点击穿透；官方定义明确限定为继续发送给**同一线程**的下层窗口。[Microsoft: WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest)
- DirectComposition 可把 composition swap chain 放入 visual tree；此 swap chain 支持由调用者选择 alpha 模式，并要求 flip presentation model。它解决 GPU 合成，不自动替应用定义“哪一个透明像素可交互”。[Microsoft: CreateSwapChainForComposition](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/nf-dxgi1_2-idxgifactory2-createswapchainforcomposition)

由此得到的实现规则是：

- **全窗口穿透模式**：使用可靠的 Win32 窗口样式切换（layered + transparent），不能只返回 `HTTRANSPARENT`。
- **角色可点、透明区穿透模式**：layered-window 路线可利用实际 alpha；composition/game-engine 路线应维护低分辨率 CPU alpha mask、碰撞多边形或角色 hitbox，并在 `WM_NCHITTEST`/引擎输入层做显式判定。后者是基于上述 API 边界的工程推论，需要原型实测。
- **拖拽**：命中角色后由宿主捕获鼠标并更新窗口位置；进入全穿透模式时必须取消捕获。无边框窗口不能期待系统标题栏拖动。

### DPI、托盘和文字输入应归宿主/控制中心负责

- 微软推荐桌面应用使用 Per-Monitor v2。跨不同缩放显示器时，窗口会收到 `WM_DPICHANGED`，应采用消息给出的建议矩形重新定位和缩放；否则角色窗口会出现物理尺寸跳变或模糊。[Microsoft: High DPI desktop development](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows) [Microsoft: WM_DPICHANGED](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-dpichanged)
- 托盘是稳定的 Win32 能力：`Shell_NotifyIcon` 负责添加、修改和删除图标；官方还要求图标本身高 DPI，并建议提供 16×16 和 32×32 资源。[Microsoft: Notification Area](https://learn.microsoft.com/en-us/windows/win32/shell/notification-area)
- 桌宠窗口经常是无焦点/点击穿透窗口，不适合承载 IME。候选词、焦点、键盘导航和可访问性应留在正常激活的 WinUI 工具面板/聊天窗口；渲染进程只接收已经提交的文本和表演命令。这样无需在特殊 overlay HWND 上重建完整文本服务链。

### Spine 4.2 资源必须与 4.2 runtime 锁步

- Spine 官方仓库同时提供 `spine-cpp`、`spine-unity` 和 `spine-godot`。官方明确建议编辑器导出版本与 runtime 版本冻结并锁步更新。[Esoteric Software: spine-runtimes](https://github.com/EsotericSoftware/spine-runtimes)
- 官方 `spine-godot` 可直接加载 `.skel`、atlas 和纹理，GDExtension 形态可放进普通 Godot 4 项目，底层基于 `spine-cpp`。[Esoteric Software: spine-godot](https://esotericsoftware.com/spine-godot)
- 因现有资产头为 Spine `4.2.33`，两个原型都应固定在官方 runtime **4.2 分支/对应发布包**；不能拿最新 4.3 runtime 是否能读入作为兼容性假设。上游也有明确案例：4.1 runtime 无法读取 4.2 导出数据。[Esoteric Software forum: Godot 4.2 runtime compatibility](https://en.esotericsoftware.com/forum/d/25218-runtimes-support-for-godot-42/20)

## 方案一：原生 Win32 + spine-cpp + Direct3D/DirectComposition

### 结构

- 一个轻量 Win32 renderer executable，创建 borderless/tool/topmost HWND。
- `spine-cpp` 4.2 解析骨骼、动画状态和 atlas；自写 Direct3D 11 批绘制器。
- 正式渲染采用 composition swap chain + DirectComposition；窗口语义原型可先用 DIB + `UpdateLayeredWindow`，尽快验证 alpha 和命中，不把临时代码当最终渲染路径。
- WinUI 控制中心通过命名管道发送 `play_animation`、`set_expression`、`set_position`、`set_click_through` 等高层命令。

### 优点

- 对 HWND、逐像素命中、捕获、拖动、topmost、tool window、DPI 和多显示器拥有最精确控制。
- 可以把透明区域的命中规则与 Spine attachment/hitbox 或 CPU alpha mask 精确结合。
- 最小依赖、可裁剪，长期空闲占用和启动时间的优化上限最高。
- 直接使用官方 `spine-cpp`，对现有 4.2 二进制 skeleton 的路径最短。

### 代价与风险

- 工程量最大：需要实现 atlas 纹理、mesh/weighted mesh、clipping、blend mode、premultiplied alpha、设备丢失、swap-chain resize 和 shader 管线。
- DirectComposition 是合成层，不替代 Spine renderer，也不替代输入命中逻辑。[Microsoft: high-performance window layering](https://learn.microsoft.com/en-us/archive/msdn-magazine/2014/june/windows-with-c-high-performance-window-layering-using-the-windows-composition-engine)
- 如果走 `UpdateLayeredWindow`，窗口每次更新要提交整张表面，且经典路径围绕 GDI DC；微软早期技术说明也指出 layered window 的逐像素能力与现代 DirectX 渲染之间存在额外桥接成本。因此它适合语义验证或小尺寸低帧率备选，不是默认的 60 FPS 最终方案。[Microsoft: Layered Windows with Direct2D](https://learn.microsoft.com/en-us/archive/msdn-magazine/2009/december/windows-with-c-layered-windows-with-direct2d)

### 适用判断

当角色窗口的极低占用、精确点击、Windows 原生行为优先级高于开发速度时，这是长期上限最高的方案；但不应在 disposable proof 通过前投入完整 renderer。

## 方案二：Godot 4 渲染宿主 + 原生 overlay/tool 控制

### 结构

- Godot renderer executable 只负责角色场景、Spine 动画、音频与局部交互。
- 使用官方 `spine-godot` 4.2 GDExtension；原生 GDExtension 或一个小 Win32 helper 负责 HWND 样式、全局点击穿透、窗口拖动与必要的 alpha/hitbox 命中。
- 托盘、设置、聊天/IME、插件和 AI 都在 WinUI 控制中心。

### 优点

- Godot 官方文档直接覆盖桌面 overlay 所需的 borderless、per-pixel transparency、transparent viewport、always-on-top、no-focus 和 passthrough polygon；这是三种方案里最快能得到“真正动起来”的原型。[Godot: Creating applications / overlay](https://docs.godotengine.org/en/latest/tutorials/ui/creating_applications.html#displaying-the-application-as-an-overlay)
- 官方 `spine-godot` 已封装 skeleton playback/manipulation，并与 Godot 2D 渲染结合，无需自写 Spine mesh renderer。[Esoteric Software: spine-godot](https://esotericsoftware.com/spine-godot)
- 动作状态机、粒子、音频、碰撞区、未来 2D/3D 配件迭代速度明显高于原生实现。

### 代价与已知坑

- Godot 文档明确记录 Windows 混合 GPU 上透明窗口存在已知问题，切 renderer 可能缓解但不是保证。[Godot overlay documentation](https://github.com/godotengine/godot-docs/blob/master/tutorials/ui/creating_applications.rst)
- Godot 的 `mouse_passthrough` 语义是传给**同一应用**的下层窗口，并非可靠跨进程/桌面穿透。2026 年上游 issue 给出了 Windows 复现，现有实用补丁通过 GDExtension 设置 `WS_EX_LAYERED | WS_EX_TRANSPARENT`；该扩展的源码是可审计的 MIT 实现，但它仍是社区补丁，不是 Godot 核心保证。[Godot issue #120881](https://github.com/godotengine/godot/issues/120881) [Godot-WinMousePassthrough](https://github.com/hubacekjakub/Godot-WinMousePassthrough)
- Windows 上动态改变 passthrough polygon 曾导致渲染裁切或闪烁；不应以“每帧重建复杂 polygon”作为最终交互设计。[Godot issue #57835](https://github.com/godotengine/godot/issues/57835) [Godot issue #80098](https://github.com/godotengine/godot/issues/80098)
- 发布包包含 Godot runtime、图形后端和 Spine GDExtension，体积与冷启动通常高于专用原生 renderer；必须由实测而不是印象决定是否可接受。

### 适用判断

这是目前最现实的首选渲染候选，但前提是 Windows 专项原型通过：透明不黑屏、双显卡不闪、跨应用点击穿透、DPI 跨屏、退出/重启和空闲占用。

## 方案三：分进程伴侣 renderer + WinUI 3 工具面板/控制中心

这不是另一种绘图库，而是应覆盖方案一或方案二的**系统边界**。

### 进程职责

| 进程 | 负责 | 不负责 |
|---|---|---|
| Control Center（WinUI 3） | 托盘、设置、工具面板、聊天与 IME、AI/插件调度、更新、renderer 监督 | 逐帧 Spine 渲染、透明角色 HWND |
| Companion Renderer | Spine/GPU、角色窗口、拖拽与 hitbox、动作状态机、音频同步 | 持久配置、插件执行、账号/AI 密钥、复杂文本输入 |

### IPC 与恢复

- Windows App SDK 桌面应用是 full-trust 进程，官方说明可使用命名管道；packaged 场景建议使用 `LOCAL\` 名称。命名对象应有显式 ACL，并考虑两端生命周期和版本变化。[Microsoft: Interprocess communication](https://learn.microsoft.com/en-us/windows/apps/develop/communication/interprocess-communication) [Microsoft: Sharing named objects](https://learn.microsoft.com/en-us/windows/apps/develop/communication/sharing-named-objects)
- 协议应是有版本的消息流，而不是共享 UI 对象：`hello/capabilities`、`load_character`、`play`、`set_pose`、`set_window_mode`、`pointer_event`、`heartbeat`、`renderer_fault`。
- 控制中心监督 renderer：异常退出后指数退避重启；保存最后稳定的角色位置与表演状态；连续崩溃进入 safe mode（关闭第三方附件/高级 renderer），而不是拖垮托盘和设置 UI。
- renderer 失联时控制中心仍可退出、改设置、查看诊断；控制中心退出时明确通知 renderer 收尾，超时后再终止子进程。

### 为什么适合未来 AI

AI 输出应先转换成受限的 `CharacterPerformance`/动作意图，再通过 IPC 交给 renderer。这样模型提供者、提示词、插件权限和文本 UI 可以演进，而渲染进程只接受有限命令；替换 Godot 为原生 renderer 时也不需要改 AI 层。

## 决策矩阵

评分 1（差）到 5（强）；“实现速度”分数越高代表越快，“包体/依赖”分数越高代表越轻。评分是基于上述官方能力与已知缺口的工程判断，必须由原型数据校正。

| 维度 | 原生 spine-cpp + DComp | Godot + spine-godot | 单进程 WinUI 内嵌 renderer | 分进程 WinUI + renderer |
|---|---:|---:|---:|---:|
| 现有 Spine 4.2 兼容路径 | 5 | 5 | 3 | 5（取决于 renderer） |
| 透明/精确命中控制 | 5 | 3 | 3 | 5（边界可专门实现） |
| 首个可用原型速度 | 2 | 5 | 2 | 4 |
| 动画/效果迭代速度 | 2 | 5 | 3 | 5（Godot renderer 时） |
| 空闲占用优化上限 | 5 | 3 | 3 | 4 |
| 包体/依赖轻量度 | 5 | 2 | 3 | 3 |
| IME/工具 UI | 2 | 1 | 5 | 5 |
| 崩溃隔离/可恢复性 | 2 | 2 | 1 | 5 |
| 可替换性/长期演进 | 3 | 3 | 2 | 5 |
| Windows 专项调试成本 | 2 | 3 | 2 | 4 |

关键点不是在“原生”和“引擎”之间一次性下注，而是先确定 **WinUI 控制面与 renderer 的稳定协议边界**，再让两个小 renderer 原型用数据竞争。

## 建议的可丢弃原型

### Proof A：Godot，目标 1–2 天

只实现：

- 载入 `NP0035_spr`，循环 `Idle_01`，触发一个 `Pat` 和一个编号表情；
- 透明、无边框、置顶的小尺寸角色窗口；
- 角色 hitbox 可拖动；热键切换“可交互/全局穿透”；
- 从一个最小 console/controller 通过命名管道切动作；
- 记录 30/60 FPS、空闲 CPU、GPU、专用/共享显存、working set、冷启动时间和发布目录体积。

硬验收：Windows 10/11；100%→150% 跨屏；Intel+NVIDIA 混合显卡（若有）；桌面、浏览器、任务栏都能正确穿透；透明边缘无黑框/白闪；renderer 被杀后 controller 能重启。

### Proof B：原生，目标 2–4 天

只实现：

- 固定 Spine runtime 4.2，读同一套 `.skel/.atlas/.png`；
- 先覆盖模型真实使用到的 attachment、blend 和 clipping；不做通用编辑器；
- 一个透明 topmost HWND、一个动作循环、拖动、全穿透切换；
- CPU alpha mask 或粗 hitbox 命中；处理 `WM_DPICHANGED`；
- 使用同一份 IPC contract 和同一套性能采集。

若 mesh/clipping 覆盖在预算内无法达到视觉一致，不扩 scope；记录缺口并结束 proof。

### Go / No-Go 门槛

优先选 Godot，除非出现任一情况：

- 目标机器上透明/混合 GPU 问题不可稳定复现与规避；
- 全局穿透和交互模式切换有可见闪烁、输入丢失或窗口样式被引擎重置；
- 空闲资源或发布体积超过项目可接受阈值；
- 4.2 GDExtension 在目标 Godot 版本上无法稳定构建/发布。

原生 proof 只有在视觉一致、输入/DPI 稳定，且可估算的剩余 renderer 工作量可控时才升级为产品实现。无论哪一个胜出，都保留分进程协议和 WinUI 控制中心。

## 最终建议

1. 把 **分进程边界** 作为本轮重构的稳定决策：WinUI 管工具、IME、托盘、AI 与恢复；renderer 管透明窗口和 Spine。
2. 先做 Godot proof，因为官方已有 Spine 与透明 overlay 路径，最快暴露 Windows 特有风险。
3. 同时保留一个很窄的原生 proof 作为性能/窗口语义基准，不立即建设完整 C++ renderer。
4. 两个 proof 必须共享同一套命令协议和验收脚本；用真实 `NP0035_spr`、真实 DPI/双显卡和跨应用点击测试决定，而不是用空场景或架构偏好决定。
