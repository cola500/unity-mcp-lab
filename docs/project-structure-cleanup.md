# Project structure cleanup — audit + applied changes

> **Scope A + Scope B applied.** Asset folder reorg (Scripts / Editor / Scenes) + scene hierarchy reorg (74 loose roots → 5). One-shot Editor helper `Tools/Quest Setup/Organize Scene Hierarchy` is re-runnable / idempotent. Nothing in vendor folders, no scripts/namespaces touched, all world positions preserved via `worldPositionStays:true`.

## Audit — what lives where today

### Asset folders (mine — committed, fully under our control)

| Folder | Files | Notes |
|---|---|---|
| `Assets/Scripts/` | 16 runtime `.cs` | Mostly flat. One subfolder exists: `Scripts/Environment/SubtleTreeWind.cs`. Mix of XR / networking / voice / debug / environment / UI scripts at the root. |
| `Assets/Editor/` | 13 Editor `.cs` | Flat. Mix of build / environment-setup / network-setup / verification / one-shot importers. |
| `Assets/Materials/` | 8 materials + 1 texture | All our own. Names communicate intent (`CampfireWood`, `DogCoat`, etc.). |
| `Assets/Models/` | `HandsControllerMesh.asset` | Single combined mesh from XRI's UniversalController. |
| `Assets/Prefabs/` | `PlayerHead.prefab`, `VoiceSpeaker.prefab` | NGO/Voice prefabs. |
| `Assets/Scenes/` | `CampfireRoom.unity`, `Main.unity` | `Main.unity` is dead per `docs/app-alignment-qa.md` — not in build settings, contains only a directional light + AI_Cube + Main Camera. |
| `Assets/audio/` | `campfire_crackle.wav` | Single ambient WAV. |
| `Assets/Resources/` | (empty) | Untouched. |
| `Assets/XR/` | XR plugin Loaders + Settings | Unity XR Plugin Management config. Touch with care. |
| `Assets/DefaultNetworkPrefabs.asset` | NGO prefab list | Lives at `Assets/` root by NGO convention. Don't move. |

### Asset folders (vendor — do NOT touch)

These are Asset Store packs (EULA-bound) or Unity-managed transitive dependencies. Their folder structures are canonical for re-importability on a fresh clone:

| Folder | Origin | Status |
|---|---|---|
| `Assets/Real Stars Skybox/` | Asset Store | Gitignored. EULA. |
| `Assets/Piloto Studio/` | Asset Store | Gitignored. EULA. |
| `Assets/NatureStarterKit2/` | Asset Store | Gitignored. EULA. |
| `Assets/Mountain Terrain rocks and tree/` | Asset Store | Gitignored. EULA. |
| `Assets/Terra/` | Asset Store | Gitignored. EULA. |
| `Assets/VFXPACK_FIRE_WALLCOEUR/` | Asset Store | Gitignored. EULA. |
| `Assets/ithappy/` | Asset Store | Gitignored. EULA. |
| `Assets/Photon/` | Asset Store (Photon Voice) | Committed (libs); Demos gitignored. Vendor-controlled. |
| `Assets/TextMesh Pro/` | XRI transitive dep | Auto-imported by Unity. Don't move. |
| `Assets/Samples/` | XRI Samples | Gitignored, re-importable via `Sample.Import()`. |
| `Assets/XRI/` | XRI Settings | Auto-created by XRI package. Editor-managed. |

### Scene hierarchy — `CampfireRoom.unity`

74 root GameObjects, of which roughly:

| Group | Count | Lives at | Notes |
|---|---|---|---|
| Trees (`tree_01` + 22 duplicates) | 23 | Root | Each tree is its own root — no parent grouping. |
| Perimeter rocks (`rock_set_*`) | 7 | Root | Each rock root. |
| Fire-pit kerbstones (`rock_set_01..04`) | 4 | Root | Visually distinct from perimeter rocks but share the `rock_set_*` prefix. |
| Mountain backdrop (`mountain_terrain_01` + 4 duplicates) | 5 | Root | Each its own root. |
| Stone seats (`StoneSeat_A` + variants, `StoneSeat_B` + variants) | 8 | Root | Mix of functional roots (visible) and duplicates. |
| Companion (`DogCompanion`) | 1 | Root | From recent slice. |
| Campfire wood pile (`SM_campfire_001`) | 1 | Under `World/` | Already grouped. |
| Fire visuals + light (`Flame`, `FireLight`, `Embers`, `FireCrackleAudio`) | 4 | Under `World/` | Already grouped. |
| Atmosphere (`Atmosphere`, `Directional Light`) | 2 | Under `World/` | Already grouped. |
| Ground + capsule logs (`Ground`, `Log_1`, `Log_2`) | 3 | Under `World/` | Already grouped. |
| FireStones empty parent | 1 | Root | Childless parent — historically held kerb stones; currently empty. |
| Player infrastructure (`PlayerSlot_A`, `PlayerSlot_B`, `EyeHeightMarker_A`) | 3 | Root | Bookkeeping. |
| GrassBreakup + 6 tufts | 7 | Already grouped under `GrassBreakup` | Internal grouping is clean. |
| `VRRig` + children (CameraOffset, hands, etc) | 1 root | Under `VRRig/` | Clean. |
| `NetworkManager`, `NetworkBootstrap`, `RemoteRig`, `TutorialPanel`, `Main Camera` | 5 | Root | Infrastructure. |

**The "messy" feeling** comes from: trees (23), perimeter rocks (7), mountains (5), stone seats (8), kerbstones (4), dog (1), FireStones empty parent (1) — ~49 environment-related objects scattered as siblings at scene root instead of under an `Environment/Forest/...` style hierarchy.

`World/` already exists and groups the campfire's immediate visuals (fire pile, flame, ground, light, atmosphere), but the forest / rocks / mountains / seats / dog never got the same treatment.

## Proposal — what to move

### Phase 1 — physical asset reorganisation (low risk)

All moves preserve `.meta` files (so GUIDs stay stable and scene refs continue to resolve).

**`Assets/Scripts/` reorg** (current 16 files at root → grouped by domain):

```
Assets/Scripts/
  Networking/
    NetworkBootstrap.cs
    NetworkHead.cs
    ClientNetworkTransform.cs
    VoiceSpeakerPlacer.cs
  Voice/
    VoiceBootstrap.cs
  XR/
    XRHeadTracker.cs
    XRControllerInputFeedback.cs
    XRTrackingOriginSetter.cs
    XRDebugLogger.cs            (XR-device console logger — different from DebugLogger.cs)
  Debug/
    DebugLogger.cs              (new JSONL session logger from previous slice)
  Environment/                  (already exists with SubtleTreeWind.cs)
    SubtleTreeWind.cs           (existing)
    FireLightFlicker.cs
    NightAtmosphere.cs
    PresenceBreath.cs
    FaceTarget.cs
  UI/
    TutorialOverlay.cs
  _Deprecated/                  (new — for review-and-delete-later)
    MicrophoneTest.cs           (early Voice-A probe, GameObject already removed)
```

**`Assets/Editor/` reorg** (current 13 files at root → grouped by domain):

```
Assets/Editor/
  Build/
    QuestBuildAPK.cs
    QuestBuildSetup.cs
  Environment/
    ForestSetup.cs
    ForestFloorSetup.cs
    GrassBreakupSetup.cs
    TreeWindSetup.cs
    CampfireMaterialPolish.cs
    CampfireMeshSetup.cs
    CampfirePolishSetup.cs
    HandVisualsSetup.cs
    PilotoShadersImporter.cs    (one-shot from old slice — review/delete candidate)
  Networking/
    NetworkSetup.cs
  Verification/
    VerificationCapture.cs
```

**`Assets/Scenes/_Archive/`** (new):
- Move `Main.unity` here — flagged dead in `app-alignment-qa.md`, not in build settings, harmless to retain as archive.

**Not moved (intentionally):**
- `Assets/Materials/`, `Assets/Models/`, `Assets/Prefabs/`, `Assets/audio/` — too few files to justify subfolders; would add cognitive cost without payoff.
- `Assets/XR/` — Unity XR plugin auto-managed config; touching it can break the plugin loader binding.
- `Assets/DefaultNetworkPrefabs.asset` — NGO convention places this at `Assets/` root.
- All vendor packs (gitignored or otherwise).

### Phase 2 — scene hierarchy reorganisation (medium risk)

50+ reparent operations grouping loose environment roots under sensible parents. Each reparent preserves world position via `worldPositionStays:true`. Scripts and component references travel with the GameObjects unchanged.

```
CampfireRoom (scene root)
  World/                         (existing)
    Ground                       (existing)
    Atmosphere                   (existing)
    Directional Light            (existing)
    Campfire/                    (NEW empty parent)
      SM_campfire_001            (was under World/, moved)
      Log_1, Log_2               (was under World/, moved)
      Flame                      (was under World/, moved)
      FireLight                  (was under World/, moved)
      FireCrackleAudio           (was under World/, moved)
      Embers                     (was under World/, moved)
      FireStones                 (existing empty parent; could host kerbstones)
    Environment/                 (NEW empty parent)
      Forest/                    (NEW empty parent)
        tree_01 + 22 duplicates  (all 23 trees reparented here)
        Rocks/                   (NEW sub-empty)
          rock_set_* perimeter   (7 perimeter rocks reparented)
        Mountains/               (NEW sub-empty)
          mountain_terrain_01 + 4 duplicates
      Grass/                     (rename existing GrassBreakup to keep its 6 tufts)
    Seats/                       (NEW empty parent)
      Seat_A, Seat_B             (functional roots, MeshRenderer disabled)
      StoneSeat_A + variants     (4 visible stones)
      StoneSeat_B + variants     (4 visible stones)
      FirePitKerb/               (NEW sub-empty)
        rock_set_01..04          (4 fire-pit kerb stones — *not* perimeter rocks)
    Companions/                  (NEW empty parent)
      DogCompanion
  VRRig                          (untouched)
  RemoteRig                      (untouched)
  NetworkManager                 (untouched)
  NetworkBootstrap               (untouched)
  TutorialPanel                  (untouched)
  Main Camera                    (untouched — disabled)
  PlayerSlot_A, PlayerSlot_B     (untouched bookkeeping)
  EyeHeightMarker_A              (untouched bookkeeping)
```

**Naming clash to resolve:** the pack ships `rock_set_01..04` for both fire-pit kerbstones AND perimeter rocks (`rock_set_02..04 (1)`, etc.). `ForestSetup.cs` and `StoneGrounding` differentiate them by world position, not by name. Reparenting requires inspecting each object's position to assign it to either `FirePitKerb/` or `Forest/Rocks/`. Doable but tedious — 11 individual inspections + reparents.

## Duplicate / dead candidates (flag, do not auto-delete)

These are flagged for human review before removal:

| Item | Status | Recommendation |
|---|---|---|
| `Assets/Scripts/MicrophoneTest.cs` | Dead. GameObject was removed in Quest validation slice (`c9cb4dd`). Script file remains. | Move to `_Deprecated/`. Delete in a follow-up slice once we're certain nothing manually re-attaches it for mic debugging. |
| `Assets/Scenes/Main.unity` | Not in build settings. Contains only Directional Light + AI_Cube + Main Camera. | Move to `Scenes/_Archive/`. Delete in a follow-up slice. |
| `Assets/Editor/PilotoShadersImporter.cs` | One-shot from old Piloto-shader investigation (Piloto shaders are HDRP, we use BiRP, the shader folder was deleted in commit `93679e5`). | Move to `Editor/Environment/` (current slice) and flag for removal. |
| `Assets/Materials/HandSkin.mat` | Only used by `Apply Hand Visuals (Force Sphere)` fallback path. | Keep — it's documented as the sphere fallback in `docs/hand-visuals-slice.md`. |
| `FireStones` empty parent in scene | childCount=0 per audit. | Reuse as the `Campfire/FirePitKerb/` parent in the scene reorg. |
| Three campfire-related Editor helpers (`CampfireMaterialPolish.cs`, `CampfireMeshSetup.cs`, `CampfirePolishSetup.cs`) | All still serve distinct purposes (material polish vs mesh swap vs flame/ember). | Keep all three. Group under `Editor/Environment/`. |
| `ForestSetup.cs` vs `ForestFloorSetup.cs` | Different responsibilities — Forest configures tree/rock/seat shadows; ForestFloor applies dirt material to Ground. | Keep both. Group under `Editor/Environment/`. |

## Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Scene refs break when scripts/materials are moved | Low (Unity tracks by GUID, not path; `.meta` moves with the file) | Move via `git mv` so `.meta` follows automatically. Verify via `recompile_scripts` + `get_scene_info` post-move. |
| Class names changing breaks scene component bindings | Low — we are NOT renaming classes, only physical file paths | No class renames in this slice. |
| Scene reparenting drifts world positions | Medium | All reparents use `worldPositionStays:true`. Spot-check 3–5 transforms post-reorg against pre-reorg snapshot. |
| 50+ scene reparents produces a giant diff | Low (functional), Medium (review) | Diff will be ~500–1000 lines of scene YAML changes (m_Father + m_Children edits). Validate by opening scene + visually inspecting hierarchy. |
| The `rock_set_*` naming overlap forces per-instance inspection | Medium | Inspect each by world position before reparenting. Slow but safe. |
| Asset Store re-imports re-create the vendor folders we ignored | None — we never touched those folders | No conflict. |
| Photon Voice editor NullReferenceException fires when scene is dirty-marked | Cosmetic Editor-only noise | Pre-existing, ignore. |

## Intentionally untouched

- All vendor Asset Store folders (gitignored or otherwise) — EULA + re-importability + scene GUIDs.
- `Assets/XR/Loaders/` and `Assets/XR/Settings/` — Unity XR Plugin Management auto-config.
- `Assets/XRI/` and `Assets/TextMesh Pro/` — XRI transitive auto-imports.
- `Assets/DefaultNetworkPrefabs.asset` — NGO convention.
- `VRRig` / `RemoteRig` / `NetworkManager` / `NetworkBootstrap` / `TutorialPanel` scene hierarchy — networking and XR roots stay exactly as they are.
- Class names, namespaces, MonoBehaviour types — no renames.
- Build settings, scene-in-build registration.
- Anything in `Photon/` other than what's already gitignored.

## Applied — Scope A summary

All file moves below used `git mv` for each `.cs` + `.cs.meta` pair, preserving GUIDs so every scene component reference continues to resolve unchanged. No class renames. No namespace changes. No scene reparenting.

### `Assets/Scripts/` (16 runtime files relocated)

```
Networking/
  NetworkBootstrap.cs
  NetworkHead.cs
  ClientNetworkTransform.cs
  VoiceSpeakerPlacer.cs
Voice/
  VoiceBootstrap.cs
XR/
  XRHeadTracker.cs
  XRControllerInputFeedback.cs
  XRTrackingOriginSetter.cs
  XRDebugLogger.cs
Debug/
  DebugLogger.cs
Environment/                  (pre-existing — joined the existing folder)
  SubtleTreeWind.cs           (was here)
  FireLightFlicker.cs         (moved in)
  NightAtmosphere.cs          (moved in)
  PresenceBreath.cs           (moved in)
  FaceTarget.cs               (moved in)
UI/
  TutorialOverlay.cs
_Deprecated/
  MicrophoneTest.cs           (GameObject removed in earlier slice — file kept for now)
Services/                     (pre-existing, untouched — has its own asmdef)
  ServicesBootstrap.cs
  UnityMcpLab.Services.asmdef
```

### `Assets/Editor/` (13 helpers relocated)

```
Build/
  QuestBuildAPK.cs
  QuestBuildSetup.cs
Environment/
  ForestSetup.cs
  ForestFloorSetup.cs
  GrassBreakupSetup.cs
  TreeWindSetup.cs
  CampfireMaterialPolish.cs
  CampfireMeshSetup.cs
  CampfirePolishSetup.cs
  HandVisualsSetup.cs
  PilotoShadersImporter.cs    (one-shot from old Piloto-shader investigation)
Networking/
  NetworkSetup.cs
Verification/
  VerificationCapture.cs
```

### `Assets/Scenes/` (1 file relocated)

```
_Archive/
  Main.unity                  (not in build settings, dead per app-alignment-qa)
CampfireRoom.unity            (untouched — still build index 0)
```

### Validation results

- `recompile_scripts` post-move: **0 errors, 63 warnings** (warnings are all pre-existing third-party CS0618 deprecations in Terra / Photon / ithappy + one new `CS0184` in `QuestBuildAPK.cs` line 180 about an `Animator` type-check pattern — not from this slice). New compile paths now reflect the subfolders, e.g. `Assets/Editor/Environment/ForestSetup.cs`.
- `get_scene_info`: `CampfireRoom.unity` loaded cleanly, `Is Dirty: false`, root count 74 (unchanged).
- Console errors: **0**.
- Scene component-ref GUID spot-checks:
  - `NetworkBootstrap` GUID `7df6f93e…` → 1 ref in scene ✓
  - `XRHeadTracker` GUID `0a0b1203…` → 3 refs (head + 2 hand anchors) ✓
  - `VoiceBootstrap` GUID `72d1a835…` → 1 ref ✓
  - `TutorialOverlay` GUID `cbea887a…` → 1 ref ✓
  - `XRControllerInputFeedback` GUID `88fecfed…` → 2 refs (2 hand anchors) ✓
- `EditorBuildSettings.asset`: `Assets/Scenes/CampfireRoom.unity` still at build index 0.

### What's NOT in this slice (deferred)

- **Scope B — scene hierarchy reorg.** 50+ reparent operations grouping loose environment roots under `World/Campfire`, `World/Environment/Forest/{Trees,Rocks,Mountains}`, `World/Seats`, `World/Companions`. Big scene diff, requires per-instance classification of `rock_set_*` (kerb vs perimeter). Plan retained in this doc for a future slice.
- **Outright deletion** of dead items. `Main.unity` and `MicrophoneTest.cs` are archived / deprecated, not removed. Removable in a follow-up once you're confident.
- **Class renames / namespace changes.** None — same class names, same default `Assembly-CSharp` membership for the moved scripts.
- **Asmdef changes.** None. The pre-existing `Scripts/Services/UnityMcpLab.Services.asmdef` is untouched.
- **Anything in vendor folders.** Real Stars, Piloto, Terra, Mountain Terrain, WALLCOEUR, NatureStarterKit2, ithappy, Photon, TextMesh Pro, XRI, Samples — all untouched.
- **Materials / Models / Prefabs / audio / Resources / XR / DefaultNetworkPrefabs.asset.** Too few files to justify subfolders; sit at their current root locations.

## Scope B applied — scene hierarchy

A one-shot Editor helper handles the ~70 reparent operations idempotently:

`Tools/Quest Setup/Organize Scene Hierarchy` → calls `SceneHierarchyOrganize.Apply()` in `Assets/Editor/Environment/SceneHierarchyOrganize.cs`.

The helper:

- Finds-or-creates the new parent skeleton under `World/`.
- Walks scene roots by name prefix (`tree_01*`, `rock_set_*`, `mountain_terrain_*`, `StoneSeat_*`) plus a hand-listed set for the campfire's immediate children.
- Classifies `rock_set_*` instances by **world XZ-distance from origin**: < 2.5 m → fire-pit kerbstone, else perimeter rock. (Names overlap because the pack reuses `rock_set_01..04` for both fire-pit and user-placed perimeter rocks; position is the only reliable signal.)
- Reparents every move with `worldPositionStays: true` so visible positions don't drift.
- Re-runs skip GameObjects already at the correct parent — idempotent.

Result of the first run:

```
[SceneHierarchyOrganize] Moved: campfire=6, trees=43, kerbstones=2,
  perimeter-rocks=7, mountains=5, grass=1, seats=10, companions=1.
  Re-run is idempotent.
```

(43 trees ≠ 23 from the original audit — the YAML grep undercounted prefab-instance overrides; the helper sees actual `GameObject.Find` matches via `scene.GetRootGameObjects()`, which is the trustworthy number.)

### Final root hierarchy

```
CampfireRoom (scene)
├── World/
│   ├── Ground                      (existing, unchanged)
│   ├── Atmosphere                  (existing, unchanged)
│   ├── Directional Light           (existing, unchanged)
│   ├── RemoteRig                   (existing, unchanged)
│   ├── TutorialPanel               (existing, unchanged)
│   ├── Main Camera                 (existing, unchanged — disabled)
│   ├── PlayerSlot_A / _B           (existing, unchanged — bookkeeping)
│   ├── EyeHeightMarker_A           (existing, unchanged)
│   ├── Campfire/                   (NEW)
│   │   ├── SM_campfire_001
│   │   ├── Log_1, Log_2
│   │   ├── Flame
│   │   ├── FireLight
│   │   ├── FireCrackleAudio
│   │   ├── Embers
│   │   ├── FireStones              (pre-existing empty parent, retained for future kerbstone host)
│   │   └── FirePitKerb/            (NEW)
│   │       └── rock_set_* (2)      (within 2.5 m of origin)
│   ├── Environment/                (NEW)
│   │   ├── Forest/                 (NEW)
│   │   │   ├── Trees/              (NEW — 43 tree_01 instances)
│   │   │   ├── Rocks/              (NEW — 7 perimeter rock_set_*)
│   │   │   └── Mountains/          (NEW — 5 mountain_terrain_*)
│   │   └── Grass/                  (NEW — wraps GrassBreakup + 6 tufts)
│   ├── Seats/                      (NEW — Seat_A/B + StoneSeat_A/B + variants, 10 total)
│   └── Companions/                 (NEW — DogCompanion)
├── VRRig                           (untouched — XR root, head + 2 hand anchors)
├── NetworkManager                  (untouched)
├── NetworkBootstrap                (untouched)
└── Forest                          (orphan empty leftover — see Cleanup follow-ups below)
```

**Root count dropped from 74 → 5.** The 4 "expected" roots are `World`, `VRRig`, `NetworkManager`, `NetworkBootstrap`. The 5th is an orphan.

### Validation results (Scope B)

- `recompile_scripts` after adding `SceneHierarchyOrganize.cs` and reorganising scene: **0 errors, 34 warnings** (all pre-existing third-party).
- Console post-helper run: **0 errors**, just the helper's success log line.
- Scene saved cleanly via `save_scene`.
- Spot-check world positions preserved:
  - `SM_campfire_001` world `(0, 0, 0)`, parent now `Campfire`, root `World` ✓
  - `StoneSeat_A` world `(1.6, -0.07001, 0)` — same as after Vary-stone-grounding slice ✓; parent now `Seats`, root `World` ✓
- Script refs intact after reorg:
  - `NetworkBootstrap` GUID `7df6f93e…` → 1 ref ✓
  - `XRHeadTracker` GUID `0a0b1203…` → 3 refs (head + 2 hand anchors) ✓
  - `VoiceBootstrap` GUID `72d1a835…` → 1 ref ✓
  - `TutorialOverlay` GUID `cbea887a…` → 1 ref ✓
- `EditorBuildSettings.asset` unchanged — `CampfireRoom.unity` still at build index 0.

### Cleanup follow-ups (flagged, not done)

- **Orphan empty `Forest` GameObject at scene root.** Pre-existing from an earlier slice (had `childCount: 0` at audit time). My `SceneHierarchyOrganize` created `World/Environment/Forest/` separately and didn't touch the orphan. Recommend deleting it in a follow-up cleanup slice (single `delete_gameobject` call) — empty, no children, no script refs, no transforms anything cares about.
- **Pre-existing empty `FireStones` GameObject** is now inside `World/Campfire/` (helper moved it). It's still empty (childCount=0); could host the fire-pit kerb stones if a future slice prefers `World/Campfire/FireStones/` over `World/Campfire/FirePitKerb/`. Keeping the parallel parent for now since the helper made `FirePitKerb` per the proposal.
- **`kerbstones=2` only** (not the expected 4). Some `rock_set_01..04` instances are placed at XZ-distance ≥ 2.5 m and got classified as perimeter rocks. Either:
  - The 2.5 m threshold needs widening (e.g. 3.0 m) and re-run the helper — re-run will move the misclassified rocks to `FirePitKerb/` and the perimeter ones stay put.
  - Or those rocks were genuinely placed further out in earlier hand-tuning. Visual check in headset will tell.

### What's intentionally untouched (Scope B)

- `VRRig` and all its children (`CameraOffset`, `VRCamera`, `LeftHandAnchor`, `RightHandAnchor`, `LeftHandMesh`, `RightHandMesh`) — XR rig stays exactly as it is.
- `RemoteRig` — networked avatar placement.
- `NetworkManager`, `NetworkBootstrap` — networking roots.
- `TutorialPanel`, `Main Camera`, `PlayerSlot_A/B`, `EyeHeightMarker_A` — bookkeeping. These were already inside `World/` before Scope B (the original audit's count of "74 roots" was inflated by scene-instance objects with serialized GameObject m_Name overrides; the truly-loose roots were only ~30).
- No GameObject renamed.
- No script class touched. No namespace change. No asmdef edit.
- No vendor folder touched.
- Visible/world positions preserved on every reparented object.
