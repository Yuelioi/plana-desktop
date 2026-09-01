# `NP0035_spr` Spine inventory

Inspected on 2026-09-01 with the repository's bundled Spine runtime using [`tools/inspect-spine.cjs`](../../../../tools/inspect-spine.cjs).

## Summary

- Skeleton export version: Spine `4.2.33`
- Bones: 315
- Slots: 152
- Skins: 1 (`default`)
- Attachments: 220
- Animations: 37
- Events: 0
- IK constraints: 5
- Transform constraints: 34
- Bounds: approximately 1154 × 2216 Spine units

This is a fairly complete standing-character rig, not a small sprite animation. It contains substantial facial and halo state data even though it exposes only one Spine skin.

## Animation inventory

| Group | Animations | Interpretation |
|---|---|---|
| Numbered states | `00` through `20`, plus `99` | 22 clips, each approximately 3.33 seconds. These need visual preview before assigning semantic names; they are likely authored expression/state clips. |
| Base | `Idle_01` (6.67 s), `Dummy` (0.03 s), `Eye_Close_01` (0.33 s) | Normal idle, setup/no-op, and blink/eye-close behavior. |
| Look | `Look_01_M` (pose), `LookEnd_01_M`, `LookEnd_01_A`, `S_Look_01_all` | A controllable look pose plus end/reset and combined sequence. |
| Head pat | `Pat_01_M`, `Pat_01_A` (poses), `PatEnd_01_M`, `PatEnd_01_A`, `S_Pat_01_M_all` | The rig already contains the pieces required for a head-pat interaction. |
| Direction setup | `Set_Front`, `Set_Left`, `Set_Right` | Zero-duration setup poses for facing/directional state. |

### Visual preview of numbered states

A disposable Web preview rendered `00`–`20` in three seven-player contact-sheet pages. The labels below are descriptive names inferred from the rendered expression and attachment families, not official names. A single frame can reliably identify the broad emotion but not every mouth/eyebrow nuance.

| Animation | Provisional meaning | Strong data cue |
|---|---|---|
| `00` | Neutral baseline | Normal halo, `Mouth_01` |
| `01` | Neutral variation | Normal halo, alternate brow layer |
| `02` | Neutral speech 1 | `Mouth_02` |
| `03` | Neutral speech 2 | `Mouth_03` |
| `04` | Troubled / sweating | Depressed halo, sweat, lowered eye covers |
| `05` | Angry / annoyed | Angry halo, `Mouth_04` |
| `06` | Sad / depressed | Depressed halo, altered eyes, `Mouth_05` |
| `07` | Shocked / surprised | Surprise halo, wide alternate eyes, sweat, `Mouth_06` |
| `08` | Wide-eyed surprise | Alternate wide eyes with normal halo |
| `09` | Happy / excited | Animated happy halo family and wide eyes |
| `10` | Angry outburst | Angry halo and open mouth |
| `11` | Stern / displeased | Sharp halo and tense face |
| `12` | Worried / sweating | Depressed halo, sweat, `Mouth_10` |
| `13` | Dizzy / overwhelmed | Spiral eye attachment, sweat, `Mouth_11` |
| `14` | Startled with eyes shut | Surprise halo, squeezed eyes |
| `15` | Soft smile / speech | Normal halo, `Mouth_12` |
| `16` | Cheerful closed-eye smile | Closed smiling eyes, `Mouth_12` |
| `17` | Affection / love | Animated love/heart halo and stronger blush |
| `18` | Blushing / shy | Normal halo, stronger flush, raised brows |
| `19` | Serious / angry | Angry halo, darker face shadow |
| `20` | Sad speech | Depressed halo, darker face shadow, `Mouth_12` |

The preview also confirmed that instantiating 21 WebGL Spine players at once exceeds the browser's practical context limit. The contact sheet therefore pages seven players at a time; this is a prototype constraint, not a model defect.

The current renderer only discovers animation names and chooses random clips matching a small name heuristic. It does not implement the intended multi-track/stateful sequencing implied by the `_M`, `_A`, `End`, and `S_*_all` families.

## Expression and attachment surface

The single default skin contains many switchable attachments. Particularly useful groups include:

- 12 mouth shapes: `Mouth_01` through `Mouth_12`.
- Multiple left/right eye, pupil, eye-cover, eyebrow, head-shadow, blush, sweat, and flush variants.
- Halo expression families: `normal`, `happy`, `surprise` (spelled `suprise` in the data), `depressed`, `love`, and `angry`, with multiple animation-frame/alternate attachments.
- Outfit and prop layers including coat, skirt patterns, ribbons, weapon layers, hair sections, hands, and fingers.

There are no Spine event definitions, so interaction timing cannot rely on event callbacks embedded in the skeleton. Timing must use animation completion, known durations, or host-authored markers.

## Practical implications

1. The model already supports head pat and look behavior. These should be implemented as explicit state machines instead of random one-shot clips.
2. The numbered `00`–`20` clips should be rendered into a contact sheet or preview selector so their visual meaning can be labeled safely.
3. Mouth, eyes, eyebrows, blush, sweat, and halo attachments offer a richer expression system without obtaining a different model.
4. Because there is only one skin, apparent expression changes are attachment/timeline state changes rather than Spine skin selection.
5. A runtime compatibility check should remain in tooling because the skeleton reports Spine `4.2.33`; the bundled parser successfully reads it, but the checked-in vendor directory and comments still contain `4.1` naming that may be misleading.

## Reproduction

```powershell
node tools/inspect-spine.cjs `
  src/Plana.Desktop/Renderer/vendor/spine-player/spine-player.js `
  src/Plana.Desktop/Renderer/spine/plana/NP0035_spr.skel `
  src/Plana.Desktop/Renderer/spine/plana/NP0035_spr.atlas
```
