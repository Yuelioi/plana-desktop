# Arona model asset research

Checked 2026-09-01.

## Conclusion

`https://ba.svdex.moe/en/live2d` is **not itself a usable source for an Arona character model**. Its UI can download the currently selected model as a Wallpaper Engine Web package, and its source repository contains downloadable `.skel`/`.atlas`/texture assets for many memorial-lobby models, but the repository tree contains no Arona `.skel`, `.atlas`, or model texture. It contains only Arona backgrounds and voice files. The site repository describes its feature as a “Live2D Viewer”, while the implementation actually loads Spine skeletons and their matching atlases. [Viewer page](https://ba.svdex.moe/en/live2d) · [source README](https://github.com/respectZ/blue-archive-viewer#readme) · [Spine loader source](https://github.com/respectZ/blue-archive-viewer/blob/main/app/lib/live2d_viewer.tsx)

The earlier Plana research pointed to the more useful source: [`asdfdsa12/BA-Spine-Viewer-Asset`](https://github.com/asdfdsa12/BA-Spine-Viewer-Asset). That same repository has an Arona standing-character runtime bundle:

- [`new/assets/arona_spr.skel`](https://github.com/asdfdsa12/BA-Spine-Viewer-Asset/blob/main/new/assets/arona_spr.skel)
- [`new/assets/arona_spr.atlas`](https://github.com/asdfdsa12/BA-Spine-Viewer-Asset/blob/main/new/assets/arona_spr.atlas)
- [`new/assets/arona_spr.png`](https://github.com/asdfdsa12/BA-Spine-Viewer-Asset/blob/main/new/assets/arona_spr.png)

It also contains `memorial/assets/arona_workpage*` variants, but `arona_spr` is the closest counterpart to the existing `NP0035_spr` standing Plana model. [Asset directory](https://github.com/asdfdsa12/BA-Spine-Viewer-Asset/tree/main/new/assets)

## Compatibility with Plana Desktop

I downloaded the three `new/assets/arona_spr` files and ran the repository's existing `tools/inspect-spine.cjs` against the exact checked-in renderer runtime, `src/Plana.Desktop/Renderer/vendor/spine-player/spine-player.js`. Parsing succeeded without conversion.

Observed inventory:

- Spine export version: **4.2.33**
- 202 bones, 138 slots, 1 skin, 204 attachments
- 44 animations, 0 events
- bounds approximately 1011 × 2128

This is the same export version as the bundled Plana model (`NP0035_spr`, Spine 4.2.33), documented in [`plana-spine-inventory.md`](./plana-spine-inventory.md). It is therefore format-compatible with the project's current Spine/Godot rendering path. Character behavior still needs an Arona-specific animation mapping because animation names/counts and rig structure differ from Plana; binary compatibility does not make the existing `PlanaPerformancePlanner` semantically portable.

## What the SVDex site contributes

The SVDex repository is useful as implementation evidence and as a bulk memorial-lobby downloader: its README says the Rust updater fetches JP/EN data and that the web UI can emit Wallpaper Engine Web packages. Its loader first tries `@esotericsoftware/spine-pixi` and falls back to `pixi-spine` for exports older than 4.2. [README fetching/download instructions](https://github.com/respectZ/blue-archive-viewer#fetching-data) · [loader implementation](https://github.com/respectZ/blue-archive-viewer/blob/main/app/lib/live2d_viewer.tsx)

However, it should not be presented as the Arona download source. The source-owned data tree has no Arona model triple, and the repository does not link to `BA-Spine-Viewer-Asset`; it credits extraction tools instead. The direct Arona bundle is available from the earlier asset repository, independently of SVDex.

## Recommendation

Use `arona_spr.skel` + `arona_spr.atlas` + `arona_spr.png` as a local development fixture for the character-pack loader. Keep production distribution user-import based unless explicit permission is obtained: the asset repository has no independent open-source license for the game assets, and its viewer describes Blue Archive assets as belonging to Nexon Games. [Viewer disclaimer](https://github.com/asdfdsa12/BA-Spine-Viewer#readme)

Prior local research consulted: [`plana-model-assets-research.md`](./plana-model-assets-research.md), [`plana-spine-inventory.md`](./plana-spine-inventory.md), and [`companion-host-architecture-research.md`](./companion-host-architecture-research.md). `E:\projects\yozya\pln\flightdeck` contains no model-source/download notes and does not mention SVDex.
