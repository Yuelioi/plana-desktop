# 普拉娜角色模型资源与格式调查

调查日期：2026-09-01。目标是判断哪些公开可发现的资源对 Plana Desktop 真正有用，重点是格式、完整度和集成成本。这里不记录客户端提取、解密或绕过方法。

## 结论

1. **游戏内普拉娜资源是 Spine，不是 Cubism Live2D。** 社区维护的 Blue Archive Spine 文件名索引把 `NP0035_spr` 明确映射为 Plana；公开 viewer 的说明也把其支持对象称为 “Standing Illust / Memorial Lobby & Story Live2d”，但实际资产包和播放器使用的是 Spine。[文件名索引](https://gist.github.com/Agent-0808/f1a52ffab7b7a8e50075b061463de60b#file-ba_spinefilenames-md)、[BA-Spine-Viewer](https://github.com/asdfdsa12/BA-Spine-Viewer)
2. **目前仓库已有的三件套就是最直接、最低成本的运行时资源：** `NP0035_spr.skel`（二进制骨骼/动画）、`NP0035_spr.atlas`（图集描述）、`NP0035_spr.png`（贴图）。公开资产仓库本身按 `memorial`、`new`、`old/assets` 分类存放游戏资源，而 viewer 指向该仓库作为资产来源。[资产仓库](https://github.com/asdfdsa12/BA-Spine-Viewer-Asset)、[viewer 说明](https://github.com/asdfdsa12/BA-Spine-Viewer#readme)
3. **没有找到官方公开的普拉娜可编辑工程。** 在 Blue Archive / Nexon / Yostar 可检索页面中能找到普拉娜的活动、图片和视频，但没有找到 `.spine`、`.cmo3`、分层 PSD 或 Cubism 模型工程下载。这里应理解为“截至调查日未发现”，不是对所有未索引页面的绝对不存在证明。官方公开内容的典型例子是周年纪念插画附件和官方视频，而非模型源文件。[Nexon 三周年公告](https://forum.nexon.com/bluearchive-en/board_view?board=3218&thread=2679788)、[Nexon Rap Battle 公告](https://forum.nexon.com/bluearchive-en/board_view?board=3218&thread=2537994)
4. **没有找到公开的普拉娜 Cubism 模型（`.moc3` / `.model3.json` / `.cmo3`）。** 能找到的公开 Blue Archive Cubism 工程是第三方制作的阿罗娜模型，仓库明确只列出 `arona.cmo3`、PSD 和 Cubism 4.1.04，不含普拉娜；它只能证明“自己重画、分层、绑定”这条路线可行，不能直接替代普拉娜资源。[stories2/BlueArchive](https://github.com/stories2/BlueArchive)
5. **Wallpaper Engine 项目值得借鉴其交互和场景代码，但不能仅凭标题认定它是 Cubism。** 原作者页面将项目标为 `Type: Web`、动态分辨率、24.438 MB，并称其为 Arona/Plana “Work Live2D”；这更像 HTML/JS Web 场景的包装类型。“L2D”在这里是表现名称，不是文件格式证据。[Workshop 原页](https://steamcommunity.com/workshop/filedetails/?id=2959252463)

因此，对当前仓库最实用的选择不是继续寻找 `.moc3`，而是：**继续使用现有 Spine 三件套；若想要更丰富的官方动作，优先核对同一 `NP0035_spr.skel` 内已有动画和皮肤；若想从根本上改变画风或实现面捕参数，则单独制作/委托一份 Cubism 普拉娜。**

## 资源清单与技术判断

| 资源 | 实际格式与完整度 | 可编辑性 | 对本项目的价值 |
|---|---|---|---|
| 游戏/社区公开的 `NP0035_spr` | Spine 运行时三件套：二进制 `.skel` + `.atlas` + `.png`；文件名索引明确对应 Plana。[索引](https://gist.github.com/Agent-0808/f1a52ffab7b7a8e50075b061463de60b#file-ba_spinefilenames-md) | 可直接播放、枚举动画、换皮肤或调混合；**不是** Spine Editor 的 `.spine` 作者工程，不能无损回到原始网格、约束和制作时间线 | **最高**。当前 WebView2 + Spine Player 已经完全对口，不必换渲染栈 |
| BA-Spine-Viewer / Asset | Viewer 明确支持立绘、纪念大厅和剧情 Spine，包含语音选择、部件透明度控制和图片导出；资产在独立公开仓库。[Viewer](https://github.com/asdfdsa12/BA-Spine-Viewer#readme)、[Asset](https://github.com/asdfdsa12/BA-Spine-Viewer-Asset) | Viewer 源码可研究；资产是运行时导出物，不是作者工程 | 可用于核对动画列表、slot/skin、透明部件和展示逻辑；无需移植整个 viewer |
| Xenon257R “Arona and Plana - Fully Interactive L2D” | Wallpaper Engine **Web** 项目；页面确认摸头、鼠标跟随、语音、角色移动、昼夜与双角色切换。[Workshop](https://steamcommunity.com/workshop/filedetails/?id=2959252463) | Workshop 页面没有列出 `.moc3`、`.model3.json` 或 `.spine`，因此仅从页面无法确认内部角色数据格式 | 交互行为和 UI/场景逻辑有参考价值；若本机已合法订阅，可只读检查 `project.json`、入口 HTML/JS 与资产后再决定是否复用 |
| stories2 的 Arona Live2D | 真正的 Cubism 4.1.04 工程，包含 `arona.cmo3`、多个 PSD 和 pose 文件；仅 Arona。[仓库](https://github.com/stories2/BlueArchive) | `.cmo3` 是可编辑 Cubism 工程，PSD 可重绘；MIT 文件覆盖仓库代码/工程的许可声明，但 Blue Archive 角色权利仍独立存在。[LICENSE](https://github.com/stories2/BlueArchive/blob/main/LICENSE) | 适合作为“自制普拉娜 Cubism”的工程结构参考，不是现成普拉娜替换件 |
| Kirito BOOTH Plana ver1.21 | 付费 MMD 3D 模型，分别提供 `.pmx`、`.blend` 或组合包，包含物理、骨骼和口型 morph。[作者商品页](https://booth.pm/ja/items/6241037) | Blender 版可深度编辑；PMX 版适合 MMD。商品页未公开一份足够明确的再发布许可文本，购买前应向作者确认应用内打包 | 需要新增 3D 渲染器，集成成本高；如果目标转为 3D 桌宠，它比把 2D Spine 硬改成 3D 更实际 |
| CGTrader “Plana Cute Anime character” | 免费 `.blend`（Blender 4.2 / Eevee），Rigify 骨架、多个面部/口型 shape keys、材质与纹理，约 142,932 polygons；页面标为 Royalty Free License (no AI)。[作者商品页](https://www.cgtrader.com/free-3d-models/character/woman/blue-archive-plana) | Blender 源文件可编辑、可重定向动画；模型较重，需要为实时桌宠减面、整理材质和导出 glTF/VRM | 公开可得的 3D 方案中技术资料最完整，但会把项目从 Spine/WebGL 2D 推向完整 3D 管线 |
| Sketchfab / Open3DLab Plana | Sketchfab 有 Plana 页面；Open3DLab 条目指回 NicoNico `td92477` 为 original MMD model。[Sketchfab](https://sketchfab.com/3d-models/blue-archive-plana-40723457e6404d269a157db7fcdb655d)、[Open3DLab](https://open3dlab.com/project/a7ae8881-a929-4d61-9363-1c4e2291043b/) | 搜索结果无法可靠确认下载包格式、骨骼完整度或许可；Open3DLab 更像二次转载入口 | 只能列作候选，不能在未核对原作者页和实际包前作为工程依赖 |

## `NP0035_spr` 能做到什么、缺什么

`.skel` 是 Spine 的二进制运行时导出格式。官方 Spine runtimes 文档/变更记录把 `.skel` 与 JSON 并列为 skeleton data，并说明播放器通过 `skelUrl` 加载二进制骨骼数据。[Spine runtimes CHANGELOG](https://github.com/EsotericSoftware/spine-runtimes/blob/4.2/CHANGELOG.md)

对 Plana Desktop，这意味着：

- 可以用与导出版本匹配的 Spine runtime 直接播放全部内嵌动画。
- 可以在运行时读取 animation、skin、slot、event 名称，做随机待机、摸头、表情触发和动作混合。
- `.atlas` + `.png` 已经提供渲染所需的贴图区域，因此不需要 PSD。
- 但它不包含可直接在 Spine Editor 中继续制作的 `.spine` 项目；若要新增真正的骨骼、网格权重或约束动画，通常要重建作者工程，不能把 `.skel` 当成等价源文件。

社区的通用 SpineViewer 也印证了这种运行时可用性：它支持 Spine 2.1.x 到 4.2.x、动画/皮肤/轨道查询及导出，并有 CLI；它适合作为版本识别和资源盘点工具，而不是生产渲染器替代品。[ww-rm/SpineViewer](https://github.com/ww-rm/SpineViewer)

## Wallpaper Engine 项目的真实可用程度

Workshop 页面能可靠确认的只有：

- 它是 `Type: Web`，不是 Wallpaper Engine 的 `Scene` 或 Video 类型。
- 它实现了摸头、鼠标跟随、语音、移动、昼夜与 Arona/Plana 切换。
- 作者在页面评论中明确同意一位用户将其作为独立 Node.js 应用的基础，并要求链接原 Workshop 页署名。这条回复很可能正对应当前项目或同类项目，但它是对特定请求的页面回复，不等同于给所有第三方资产发放通用许可证。[Workshop 评论](https://steamcommunity.com/workshop/filedetails/?id=2959252463)

页面**不能**证明的内容：

- 内部是 Cubism `.moc3`、Spine `.skel`，还是序列图/自定义 sprite 动画。
- 是否包含可编辑源工程（`.cmo3` / `.spine` / PSD）。
- Workshop 项目作者是否拥有底层游戏图像和语音的再授权权利。

本地个人使用场景下，最有效的下一步是对已订阅的 Workshop 包做一次**只读格式清点**：记录 `project.json` 类型和入口，搜索文件扩展名（`.moc3`、`.model3.json`、`.skel`、`.atlas`、`.json`、`.js`、音频），再比较其交互状态机与当前 `Renderer/app.js`。无需也不应执行任何解密步骤。若它同样使用 `NP0035_spr` Spine，价值主要在交互代码和动作编排；若意外包含真正 Cubism 数据，才值得评估新增 Cubism SDK。

## 对当前仓库的集成建议

当前项目是 .NET 10 + 原生 Win32/Windows Composition + WebView2，Web 页面中使用 Spine Player。这个架构与 `.skel/.atlas/.png` 完全匹配，因此建议按下面顺序推进：

1. **先盘点现有 `.skel`。** 在开发工具或一次性脚本中列出 animation、skin、slot、event，确认官方运行时资源实际已经包含多少动作；很多“需要找更完整模型”的需求可能只是现有动作没有接到 UI。
2. **复刻 Workshop 交互，不先换模型格式。** 摸头命中区、眼睛跟随、随机语音、昼夜状态、对话框跟随都能围绕当前 Spine runtime 实现；这是最小改动路线。
3. **把模型提供者做成资源适配层。** 继续以 Spine 为默认 provider，未来若真的取得 Cubism `.model3.json + .moc3 + textures + motions`，再新增 Cubism provider，而不是重写宿主窗口。
4. **3D 作为独立实验分支。** `.blend/.pmx` 不应直接塞进现有 Spine Player。实用路线是 Blender 整理后导出 glTF/VRM，再在 WebView2 中接 Babylon.js/Three.js，或引入原生/Unity/Godot 子宿主；透明窗口、拾取、性能和打包成本都会明显上升。
5. **如果目标是“可编辑 2D 原文件”，应自制或委托。** 需要的交付物应明确写成：分层 PSD、`.cmo3`、运行时 `.moc3`、`.model3.json`、textures、physics、expressions、motions，以及允许本地应用使用/是否允许随安装包分发。现有公开搜索未找到这样的普拉娜套件。

## 简短的使用边界

个人本地使用的技术可行性和公开分发是两回事。BA-Spine-Viewer 自己声明是非商业 fan viewer，并说明所有资产属于 Nexon；其资产仓库没有显示独立开源许可证。[Viewer README](https://github.com/asdfdsa12/BA-Spine-Viewer#readme)、[Asset repo](https://github.com/asdfdsa12/BA-Spine-Viewer-Asset) 因此本地开发可继续验证，但若以后发布安装包，最好把角色资源改为用户本地导入，或取得明确授权。Workshop 作者对应用改造的积极回复有帮助，但不能替代底层 Blue Archive 资产权利人的许可。

## 最终推荐

**短期：保留 `NP0035_spr` Spine，做动画/slot/event 全量盘点，并研究 Workshop Web 项目的交互编排。** 这是现有代码几乎零迁移成本、效果提升最大的方向。

**中期：若现有 Spine 动作不够，定制一份普拉娜 Cubism 工程。** 公开搜索没有发现现成完整的普拉娜 Cubism 原文件；与其等待不确定资源，不如以 stories2 的 Arona 工程作为交付结构参考。

**只有明确想要 3D 时，才选择 BOOTH 或 CGTrader 的 Blender/MMD 模型。** 它们确实更可编辑，但不是“更方便替换现有模型”，而是一次渲染架构升级。
