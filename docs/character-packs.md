# Character Packs

A Character Pack is a declarative folder that supplies one Companion character's Spine assets, layout, and semantic performance mapping. It does not execute code and does not contribute Actions.

## Folder layout

```text
arona/
  character.json
  arona_spr.skel
  arona_spr.atlas
  arona_spr.png
```

The atlas may reference multiple texture pages. Every referenced texture must remain inside the Character Pack folder.

## Manifest

```json
{
  "schemaVersion": 1,
  "id": "example.arona",
  "name": "Arona",
  "version": "1.0.0",
  "skeleton": "arona_spr.skel",
  "atlas": "arona_spr.atlas",
  "layout": {
    "x": 320,
    "y": 835,
    "scale": 0.36,
    "hitPolygon": [
      { "x": 0.2, "y": 0.3 },
      { "x": 0.8, "y": 0.3 },
      { "x": 0.8, "y": 1.0 },
      { "x": 0.1, "y": 1.0 }
    ]
  },
  "performance": {
    "idle": "Idle_01",
    "speaking": "02",
    "emotions": {
      "Neutral": "00",
      "Happy": "12",
      "Worried": "19"
    },
    "gestures": {
      "Blink": "Eye_Close_01",
      "HeadPat": "Pat_01_M",
      "LookAtPointer": "Look_01_M"
    }
  }
}
```

`layout.x`, `layout.y`, and `layout.scale` use the Renderer project's 640×900 canvas. Hit-polygon coordinates are normalized from 0 to 1 against the Companion window.

Emotion keys may use `Neutral`, `Happy`, `Excited`, `Surprised`, `Sad`, `Worried`, `Angry`, `Affectionate`, `Shy`, and `Dizzy`. Gesture keys may use `Blink`, `HeadPat`, and `LookAtPointer`. Missing mappings fall back to the neutral expression and required idle animation.

## Install and select

Open **Settings → Character**, choose **Import Character Pack**, and select the folder containing `character.json`. Plana copies the validated folder into `%LOCALAPPDATA%\PlanaDesktop\characters\<pack-id>` and lists it in the Character picker. Selecting it restarts only the supervised Renderer; settings, Plugins, AI conversation, and Actions remain in the Host.

Invalid or missing selections fall back to the bundled Plana Character Pack. Imported packs cannot override the bundled `builtin.plana` identity.

## Release distribution

Plana itself is bundled with the main application and must not be downloaded separately. Releases may attach a `.planacharacter` bootstrap package containing the manifest plus HTTPS source URLs and required SHA-256 hashes. Users download that small file, choose **Import Character Pack**, and Plana downloads, verifies, validates, and installs the assets. Folder import remains available for local authoring.

The Control Center also ships a searchable **Get more characters** catalog. Selecting an entry downloads the same verified bootstrap assets and installs the Character Pack without requiring users to find a release file manually. The catalog has three explicit resource classes: **Character only** for transparent full-motion standing models (bundled Plana and downloadable Arona), **Animated scenes** for 273 Memorial Lobby `_home` models with their original backgrounds, and **Static illustrations** for 658 transparent story `_spr` models. Installed entries remain visible with an Installed label and immediately appear in the searchable Character picker.

Bootstrap packages allow at most eight HTTPS assets, require every declared SHA-256 or pinned Git blob SHA-1 to match, cap each response at 25 MB when the server reports its size, contain every target path inside the temporary package directory, and still pass the ordinary Character Pack validator before installation.

## Safety and rights

Character Packs are passive data. Absolute paths, traversal outside the pack, missing atlas textures, unsupported schema versions, unknown semantic keys, and invalid normalized hit polygons are rejected.

Only import or publish assets you are allowed to use. A viewer or community asset repository proving technical availability does not grant redistribution rights. Plana Desktop does not bundle third-party Blue Archive Character Packs, and a public Release must not attach Arona or other game assets without permission from the applicable rights holders.
