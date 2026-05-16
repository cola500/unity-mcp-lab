# Controller / hand visual audit

> Investigation-only. The current local hand visuals work but feel dated in headset. Catalogues what's in the project today, what's available without big downloads, and which direction is worth a tiny prototype slice.

## Current state

Local hand visuals live at `VRRig/CameraOffset/{LeftHandAnchor,RightHandAnchor}/{Left,Right}HandMesh`. Verified in scene + via `Assets/Prefabs/PlayerHead.prefab` (same mesh + material now used for the *remote* avatar's hands per the remote-avatar-polish slice).

| Property | Value | Note |
|---|---|---|
| Mesh | `Assets/Models/HandsControllerMesh.asset` | Combined `Mesh.CombineMeshes` output from `XR Interaction Toolkit/Samples~/Starter Assets/Models/UniversalController.fbx`. ~703 KB asset on disk → roughly 5 k triangles per hand. |
| Material | `Assets/Materials/HandController.mat` | Standard shader (Built-in RP). Color `(0.22, 0.20, 0.18)` warm dark grey. Metallic `0`, smoothness `0.35`. No `_MainTex`, no `_BumpMap`, no `_DetailAlbedoMap`. |
| `localScale` | `(0.9, 0.9, 0.9)` | Uniform — fits a real Quest Touch silhouette. |
| `localEulerAngles` | `(45, 0, 0)` per the original hand-visuals slice; the live transform reads `(0, 180, 0)` after some downstream re-edit | World rotation comes out `(45, 90, 0)` — pitch forward + parent yaw. The +45° pitch was tuned in `docs/vr-alignment-polish.md` so the pointer ray dips down from the tracked grip pose, matching how a held Touch controller actually angles. |
| `shadowCastingMode` | `Off` | Same on both hands; remote avatar prefab too. |
| Collider | `BoxCollider` | Inert — no physics callback path uses it. |
| Networking | None on local | Remote hands sync via four `NetworkVariable<Vector3>`/`<Quaternion>` in `NetworkHead.cs`; the local mesh is purely visual. |
| Input ray / interactor | Not present | We have no `XRRayInteractor`, no laser, no curved beam. Input is the four-button + thumbstick polling in `NetworkBootstrap`. Changing the visual mesh has zero impact on input. |

### Why it feels "old"

- **Single matte colour, no texture / normal / detail.** A 5 k-triangle controller silhouette with no surface variation reads as a "primitive prop" rather than something physical. Firelight flicker has nothing to grip onto.
- **Generic XRI shape.** UniversalController is a `Quest 2 / Touch Pro` family stand-in but not specifically the Quest 3 Touch (no Touch Pro tracking ring; Touch 3 has a different curved grip). Anyone testing on Quest 3 sees a controller that doesn't quite match the one in their hand.
- **No finger / hand suggestion.** The cozy VR genre (Bigscreen, AltspaceVR back when, Pillow, Cozy Grove for Quest) tends to use abstract glove / mitten silhouettes rather than literal controllers — friendlier across hardware.

## Alternatives audit

### What's already in the project

| Asset | Where | Notes |
|---|---|---|
| `UniversalController.fbx` | `Library/PackageCache/com.unity.xr.interaction.toolkit@.../Samples~/Starter Assets/Models/` (imported into `Assets/Samples/XR Interaction Toolkit/3.5.0/Starter Assets/Models/`, gitignored, re-importable) | What we use today. Generic Touch-family shape. |
| `LeftHandQuestVisual.prefab` / `RightHandQuestVisual.prefab` + `AndroidXRVisual` variants | `Library/PackageCache/com.unity.xr.interaction.toolkit@.../Samples~/Hands Interaction Demo/Prefabs/` | **Reference a base prefab GUID that lives in `com.unity.xr.hands`** — that package is NOT installed (`grep com.unity.xr.hands` in `Packages/manifest.json` returns nothing). Without installing that package, these prefabs render as missing references. |
| `Unity_Hand_Light.mat` / `_Medium.mat` / `_Dark.mat` | Same sample folder | Three skin-tone materials shipped alongside the hand prefabs. Useful if we ever install the hands package. |
| `Animals_FREE` atlas | `Assets/ithappy/` (gitignored) | Stylized animals, no human hands. |
| Other vendor packs (NatureStarterKit2, Mountain Terrain, Piloto, Terra, Real Stars, WALLCOEUR) | gitignored | None contain hand or controller meshes. |

### What we could install

| Option | Package | Approx size | What you get |
|---|---|---|---|
| **Unity XR Hands** | `com.unity.xr.hands` + `Hands Visualizer` sample | ~3 MB package + ~15 MB sample | Skinned hand FBX (rigged for 26-bone hand tracking). Includes the canonical Unity stylized "white VR hand" mesh and a controllerless display. Bones default to T-pose without hand-tracking data; would need to author a static rest-pose for controller use. |
| **OpenXR Plugin** (`com.unity.xr.openxr`) + `Controller Model` runtime feature | ~5 MB | Pulls real Quest Touch 3 mesh from the runtime via `XR_MSFT_controller_model`. Requires swapping from `com.unity.xr.oculus` → OpenXR — that's an architectural switch, not a polish. |
| **Meta XR Core SDK** (`com.meta.xr.sdk.core`) | ~100 MB+ | Accurate Quest 3 Touch + Touch Pro models, hand-tracking, interaction system. Heavy. The hand-visuals slice explicitly ruled this out as "huge SDK". |
| **CC0 low-poly hand mesh** (Sketchfab / Quaternius / Kenney) | <1 MB | Author-friendly cartoony hands. Need to find one, verify license, import. No package install. |

## Option comparison

| | A. Keep current + polish material | B. Install XRI Hands sample | C. Procedural cozy mittens | D. Import CC0 low-poly hand | E. Meta XR Core | F. OpenXR controller-model API |
|---|---|---|---|---|---|---|
| Visual quality | Marginal improvement (texture detail at most) | Real hand silhouette, but T-pose without tracking | Stylized, fits cozy theme | Stylized + clean | Most accurate | Authoritative real Quest 3 Touch |
| Implementation risk | Very low | Medium (package add + bones + pose authoring) | Low–medium (~80 lines C# in the same Editor-helper pattern we already use) | Medium (license verification + import + UV check) | High (huge SDK, scope creep) | High (rig backend swap) |
| Package size cost | 0 | +3 MB + +15 MB sample | 0 | <1 MB | +100 MB+ | +5 MB but architecture change |
| Quest perf | Same | Skinned mesh + Animator → slightly higher cost | Lowest (procedural mesh, single material) | Low | Low (well-optimised) | Same as current |
| Compatibility with current XR rig | Drop-in | Drop-in if we keep tracking off | Drop-in | Drop-in | Drop-in (own SDK does extra setup, but visuals can be isolated) | Requires switching XR plugin |
| Multiplayer / NetworkHead impact | None | None (NetworkHead writes pos/rot only) | None | None | None | None |
| "Cozy fika" theme fit | Neutral | Realistic (slightly clashes) | Strong | Depends on style | Realistic (clashes) | Realistic (clashes) |

## Recommendation

**Two viable next slices, pick one based on aesthetic intent:**

### Recommended — Option C: procedural cozy mittens (~60–90 min slice)

Best fit for CampfireVR's existing visual language. Authored in code as an Editor helper (`MittenHandsSetup.cs`) under `Editor/Environment/`:

- One sphere palm (~16 tris)
- Four capsule fingers (~20 tris each)
- One thumb capsule, slightly rotated
- All baked via `Mesh.CombineMeshes` into a single new `Assets/Models/MittenHand.asset`
- Single Standard-shader material in warm wool tone (e.g. `(0.45, 0.38, 0.32)`) — matches the campfire's bark / log / dog colour family

Apply to both `LeftHandMesh` and `RightHandMesh` plus `PlayerHead.prefab`'s `Left/RightHandVisual` (so local + remote stay visually unified, as the remote-avatar-polish slice intended).

Keep the current `HandsControllerMesh.asset` + `HandController.mat` as a documented fallback (similar to how `HandSkin.mat` is the sphere-fallback for the Force-Sphere menu).

Risks:
- Procedural geometry can look stiff. Will need 1–2 headset iterations to tune proportions (finger length, palm width).
- Without finger animation, the fingers stay in a fixed "relaxed grip" pose. Fine for static-controller use; would look stiff if we ever did hand-tracking.

### Alternative — Option B: install `com.unity.xr.hands` for the canonical Unity hand mesh (~2 hour slice)

Real skinned hand silhouette. Authentic VR look. But:
- Needs bones to look right — without hand-tracking pose data, fingers default to T-pose or a rest pose that the asset author chose.
- Adds ~18 MB to the project (3 MB package + 15 MB sample), all of which can be gitignored once we extract a single rest-pose mesh.
- Would unblock proper hand-tracking later if we ever add it.

Recommend this if you specifically want "looks like a Real VR Hand" over "looks intentionally stylized".

## Deferred options

- **Meta XR Core SDK** — ruled out for now (hundreds of MB, scope creep). Worth revisiting only if we go to the Meta Quest Store and need accurate Touch 3 / Touch Pro silhouettes for marketing screenshots.
- **OpenXR controller-model API** — defer until we have a reason to swap XR providers. The Oculus plugin works fine and the controller mesh is a polish issue, not a tracking issue.
- **CC0 hand mesh hunt** — defer unless the procedural mitten prototype reads as too crude. The Sketchfab CC0 catalogue has some candidates (search "low poly hand", filter by CC0) but each needs license verification and UV/scale work.

## Proposed next slice

If you want me to proceed: **"Add cozy mitten hand visuals"** following the existing pattern of the dog-companion + remote-avatar-polish slices:

1. Author `MittenHandsSetup.cs` in `Editor/Environment/` — generates the mesh procedurally, saves to `Assets/Models/MittenHand.asset`, creates `Assets/Materials/MittenWarm.mat`.
2. `[MenuItem("Tools/Quest Setup/Apply Mitten Hands")]` — assigns mesh + material to both `LeftHandMesh` and `RightHandMesh` plus the prefab's `Left/RightHandVisual`. Idempotent.
3. Keep `HandController.mat` + `HandsControllerMesh.asset` on disk as fallback. Add `Apply Hand Visuals (Force Controller)` menu paralleling `Force Sphere`.
4. Doc: `docs/mitten-hands-slice.md`.

Total expected diff: 1 new editor script (~120 lines), 2 new asset files (mesh + material), 1 new doc, edits to scene transforms (mesh + material refs only, no GameObject identity changes).

## What was NOT changed in this audit

This is an investigation-only slice. **No** code, scene, prefab, or material was touched. Only this document was written. Recompile clean — no scripts changed.
