# Hand visuals slice

> Tiny polish pass. `LeftHandMesh` and `RightHandMesh` were rectangular Cube primitives (~7 × 4 × 12 cm) floating in space. Now they're Unity's official **UniversalController** mesh (the Quest-Touch-style controller silhouette that ships in XRI's Starter Assets sample), combined into a single Mesh asset and tinted dark warm grey. Reads as "a controller in each hand" in headset instead of a floating rectangle.

## What's in

| Asset / change | Purpose |
|---|---|
| `com.unity.xr.interaction.toolkit` 3.5.0 (new package) | Provides the UniversalController.fbx source mesh via its Starter Assets sample. We use **only the mesh asset**; XRI's interaction system, action-based controllers, and input bindings are NOT activated. |
| `Assets/Editor/HandVisualsSetup.cs` (rewritten) | Editor helper. `Tools/Quest Setup/Apply Hand Visuals` auto-imports the Starter Assets sample on first run, then bakes UniversalController's ~10 sub-meshes into one combined Mesh asset and assigns it to both hand visuals. Plus a `Force Sphere` menu for the fallback path. |
| `Assets/Models/HandsControllerMesh.asset` (new, 703 KB) | The combined mesh — what the scene actually references. Generated from UniversalController.fbx via `Mesh.CombineMeshes(mergeSubMeshes: true)`. |
| `Assets/Materials/HandController.mat` (new) | Standard shader, dark warm grey `(0.22, 0.20, 0.18)`, smoothness 0.35. Reads as "controller body" in firelight without competing with the campfire glow. |
| `Assets/Materials/HandSkin.mat` (new, currently unused) | Standard shader, warm peach `(0.72, 0.52, 0.42)`, smoothness 0.20. Materialises only when `Force Sphere` is invoked. Kept on disk as a documented fallback per slice spec. |
| `Assets/Scenes/CampfireRoom.unity` (modified) | `LeftHandMesh` and `RightHandMesh` now reference the combined mesh + HandController material. Transform position / rotation, MeshFilter slot, MeshRenderer slot, BoxCollider, parent (`LeftHandAnchor` / `RightHandAnchor`), and `transform.GetChild(0)` auto-binding contract for `XRControllerInputFeedback` are all preserved. |
| `Assets/XRI/` (new, committed) | XRI's runtime settings — needs to be in the repo so Quest builds use the same XR Interaction config. Auto-created by the package. |
| `Assets/Samples/` (new, **gitignored**) | XRI Starter Assets sample tree (~10 MB). Re-importable via `Apply Hand Visuals` on any clone — the helper calls Sample.Import() itself. Kept out of the repo for consistency with other Asset Store source folders. |
| `.gitignore` | New `Assets/Samples/` entry. |

## The problem (before)

`LeftHandMesh` / `RightHandMesh` were Unity Cube primitives with a non-uniform scale of (0.07, 0.04, 0.12) — a flat 7 × 4 × 12 cm rectangle each. From the seat, two grey-white rectangles floating where your hands should be. The first thing anyone notices in headset, before they even look at the campfire.

The two anchors are driven by `XRHeadTracker` (XRNode.LeftHand / RightHand → controller pose). `XRControllerInputFeedback` then auto-binds `visualTarget = transform.GetChild(0)` on enable and lerps that child's localScale to `_baseScale * 1.15` on trigger press. So the visual must be a child of the anchor, and changing its localScale is read by the feedback script — both constraints honoured by this change.

## Options checked

| Option | Verdict |
|---|---|
| **Unity XR Interaction Toolkit — Hands Interaction Demo sample** (skinned hand-meshes with fingers) | Rejected. `LeftHandQuestVisual.prefab` / `RightHandQuestVisual.prefab` reference a base prefab (guid `bf7151579c38e2a44be94ba8773876c1`) that lives in `com.unity.xr.hands` — a separate package we'd have to install. Plus they're SkinnedMeshRenderer rigs expecting hand-tracking pose data, which the spec explicitly rules out ("Do NOT add hand tracking yet"). |
| **Unity XR Interaction Toolkit — Starter Assets sample → UniversalController.fbx** | **Selected.** Standalone (no extra packages), 317 KB FBX, official Unity controller mesh. Static MeshFilter geometry that we combine into one mesh and use straight. Real controller silhouette in <30 MB total cost. |
| **Meta XR Core SDK** (high-quality hand prefabs) | Rejected. Hundreds of MB. Violates "Do NOT import a huge SDK just for hands". |
| **CC0 hand mesh from Sketchfab / Polyhaven / similar** | Rejected. Out-of-band download, manual license verification, and no source identified that's already in the project's neighbourhood. XRI's controller mesh fills the "need a real mesh, no extras" niche better. |
| **Procedural composite (sphere palm + capsule fingers in code)** | Rejected. Still primitive-based, more code than the helper that just imports a real FBX. The user explicitly preferred "real hand meshes if possible". |
| **Asset Store-cache scan** | No relevant pack — local cache has only Cartoon VFX, Denis Pahunov, Geoff Dallimore, Jermesa, Photon, Piloto, Shapes, Walker C (publishers we've been using for environment / VFX / audio / networking, none for hands). |
| **Existing imported packs** (Piloto, Terra, NatureStarterKit2, Mountain Terrain, WALLCOEUR, Real Stars) | No hand / glove / finger / arm assets in any of them. |

## Why "controller-mesh" instead of "real hand"

The spec opened the door to importing a small lightweight hand asset, but every real hand-mesh path (XRI Hands Demo, Meta XR SDK, OpenXR Hands) requires either an extra runtime package or an active hand-tracking subsystem — both excluded by spec. The UniversalController is the largest step toward "real geometry under each hand" that's reachable without crossing those lines:

- Standalone mesh, no extra runtime dependencies
- Already designed to sit at a controller's grip pose (which is what `XRHeadTracker` already tracks)
- Reads correctly in firelight: dark, matte, recognisable Touch-controller silhouette
- ~5,000 triangles after combine — Quest-friendly

If we later want anatomical hands with finger animation, that's a separate slice: install `com.unity.xr.hands`, import Hands Interaction Demo, swap the FBX path in this helper, and add a default static hand pose. The architecture supports it; the scope here doesn't.

## Editor helper

`Tools/Quest Setup/Apply Hand Visuals` (in `Assets/Editor/HandVisualsSetup.cs`):

1. **Ensure Starter Assets sample is imported.** Looks up `Sample.FindByPackage("com.unity.xr.interaction.toolkit", "")`, finds the "Starter Assets" entry, and calls `Sample.Import(OverridePreviousImports)` if `isImported == false`. After triggering an import it logs "re-run once Unity finishes refreshing" and exits — Unity reimports the copied FBX on the next domain pass, so the second run picks up a fully-resolved asset.
2. **Locate `UniversalController.fbx`.** Walks `Assets/Samples/XR Interaction Toolkit/<version>/Starter Assets/Models/` recursively (the version sub-folder is fixed by Unity at import time and we don't hard-code it).
3. **Combine meshes.** Instantiates the FBX into a hidden GameObject, collects every `MeshFilter`, builds a `CombineInstance[]` with each filter's `sharedMesh` + `localToWorldMatrix`, and runs `Mesh.CombineMeshes(mergeSubMeshes: true)`. The temp instance is destroyed before saving. Index format is forced to `UInt32` as a safety guard in case the FBX has > 65k vertices.
4. **Save combined mesh asset** at `Assets/Models/HandsControllerMesh.asset`. Re-uses the existing asset slot if present (via `EditorUtility.CopySerialized`) so references stay stable across re-runs.
5. **Create / refresh HandController.mat** with the dark-warm-grey tint.
6. **Assign** combined mesh + material to both `LeftHandMesh` and `RightHandMesh`. Sets `shadowCastingMode = Off`, `receiveShadows = false`, and uniform `localScale = 0.9`.
7. **Mark scene dirty.**

`Tools/Quest Setup/Apply Hand Visuals (Force Sphere)` runs the original sphere-primitive path: Unity's built-in Sphere mesh, the warm-peach `HandSkin.mat`, uniform scale 0.07. Kept as a documented fallback for the case where the XRI sample can't be imported (offline machine, broken package cache, etc.) or if a future debug needs to isolate the controller-mesh as a variable.

Idempotent + reversible: re-running `Apply Hand Visuals` rebuilds the combined mesh in place; `Force Sphere` flips the visual back to a sphere at any time; `git revert` of this commit undoes the slice cleanly.

## Performance

| Metric | Value |
|---|---|
| New scene GameObjects | 0 |
| New combined mesh triangles | ~5,000 (UniversalController body + buttons + joystick + trigger) per hand visual |
| New shared materials | 1 active (HandController.mat); 1 dormant (HandSkin.mat) |
| New textures in build | 0 |
| New shader variants | 0 (Standard, no keywords beyond what other materials already compile) |
| Shadow casters added | 0 (both have `shadowCastingMode = Off`, `receiveShadows = false`) |
| Realtime light interaction | Both receive light from FireLight + scene ambient. Matte material avoids hot specular under flicker. |
| Build / runtime impact of `com.unity.xr.interaction.toolkit` 3.5.0 | Editor + thin runtime DLL (~few MB). No XRI MonoBehaviours added to the scene, so zero per-frame cost. Adds the XRI assembly to the build's link tree; stripper drops most of it. |
| `Assets/Samples/` (~10 MB) | **Not in build** — only in Editor's authoring tree. Gitignored. |

Net Quest runtime impact: equivalent to "two more static meshes in the scene" (cheaper than one log on the campfire). The XRI package adds compile time to the Editor but nothing that ships to the headset.

## What happens on a fresh clone

1. Clone the repo. `Assets/Samples/` is not present (gitignored).
2. Open the Unity project. Package Manager pulls `com.unity.xr.interaction.toolkit` 3.5.0 from the registry (locked via `packages-lock.json`).
3. `Assets/Models/HandsControllerMesh.asset` is already committed, so if the scene loads first, both hand visuals render correctly using the cached combined mesh. **The clone is visually complete without any extra steps.**
4. Re-running the slice (or rebuilding the combined mesh from source) is one menu click: `Tools/Quest Setup/Apply Hand Visuals` triggers `Sample.Import()` on the first call, then assigns on the second.

So the visual is reproducible from source (FBX), shippable from cache (combined mesh asset), and the source tree stays light.

## Reversibility

Three independent ways to undo:

1. **`Tools/Quest Setup/Apply Hand Visuals (Force Sphere)`** — instantly replaces controller-mesh with the sphere fallback, no code changes needed.
2. **In the Inspector**: open `LeftHandMesh` / `RightHandMesh`, set `MeshFilter.Mesh` back to Cube primitive (or anything else), set `MeshRenderer.Material` back to default. localScale is a manual reset.
3. **`git revert`** of this commit: removes the package, the helper, the materials, the combined mesh asset, the scene changes, and the .gitignore line in one shot. Sample folder stays on disk locally (since it was gitignored) — delete `Assets/Samples/` manually if desired.

## What was intentionally avoided

| Avoided | Reason |
|---|---|
| Instantiating XR Controller Left/Right.prefab as scene children | Prefab carries ~10 child GameObjects + several MonoBehaviours (ActionBasedController, XR Controller Manager, ContinuousMoveProvider, etc) that we explicitly don't want activating. Combined mesh is the lighter route. |
| Changing `XRControllerInputFeedback.visualTarget` binding | Auto-binds to `transform.GetChild(0)` on enable. We kept `LeftHandMesh` as the only child of `LeftHandAnchor`, so the binding still resolves to the same GameObject and `_baseScale` captures the new (0.9, 0.9, 0.9) baseline cleanly. |
| Adding a SkinnedMeshRenderer | Would require either bones + a hand-tracking pose source (out of scope) or a baked default-pose mesh that adds authoring complexity for marginal visual gain at the viewing distance. |
| Hand tracking | Spec explicit. `com.unity.xr.hands` not installed. |
| Network sync of the new mesh | The network avatar already syncs hand position / rotation per `NetworkHead.cs`. Mesh choice is local-only — each client renders whatever its scene has. If we want both players to see the same controller mesh on each other, that's already happening via the same scene asset on both sides. |
| Per-hand mirroring of the controller mesh | UniversalController.fbx is symmetric enough that the same mesh works for both hands; no `scale.x = -1` mirror needed. Touch controller's bumper / trigger / joystick aren't strongly handed at headset viewing distance. |
| Touching `NetworkHead.cs`, the remote-avatar hand cubes, or any networking code | Out of scope for this slice; remote-avatar hand visuals remain a separate known-issue per `UnityProject/README.md`. |

## Validation

- `recompile_scripts` after rewriting `HandVisualsSetup.cs`: **0 errors, 0 new warnings**. The 33 warnings logged are pre-existing CS0618 obsolete-API warnings from third-party packs (Terra, Piloto, NatureStarterKit2 transitive editors) and our own old editor helpers — none from this slice's code.
- `Apply Hand Visuals` (first run, no sample imported yet): logs `Starter Assets sample imported. Re-run 'Apply Hand Visuals' once Unity finishes refreshing.` Sample tree appears at `Assets/Samples/XR Interaction Toolkit/3.5.0/Starter Assets/`.
- `Apply Hand Visuals` (second run): logs `Updated 2 hand mesh(es) with controller-mesh (HandController, uniform 0.90× scale, shadows off).` Combined mesh asset saved (703 KB).
- `get_gameobject LeftHandMesh` and `RightHandMesh` post-apply: position `(1.6, 1.2, 0)`, rotation 270° Y (rig-frame), `localScale (0.9, 0.9, 0.9)`, parent intact, MeshFilter + MeshRenderer + BoxCollider intact, `shadowCastingMode = Off`, `receiveShadows = false`, world `bounds.size = (0.103, 0.070, 0.046)` ≈ 10 × 7 × 5 cm — Quest Touch controller proportions. ✓
- Console after both apply runs: **clean**, no errors or warnings from this slice.
- `git diff --stat`: scene + manifest + packages-lock + ProjectSettings touched; untracked are the new editor script, materials, combined mesh asset, and XRI / Samples folders (Samples gitignored).
- No `XRControllerInputFeedback` rebind needed — auto-bind still resolves to `LeftHandMesh` / `RightHandMesh` as `transform.GetChild(0)`. `_baseScale` captures `(0.9, 0.9, 0.9)` on next OnEnable; trigger-press scales to `(1.035, 1.035, 1.035)` — same 15 % feedback as before, now applied to the controller mesh.

## License

- `com.unity.xr.interaction.toolkit` is published by Unity Technologies under the [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license) for use with Unity-developed runtimes. Standard for first-party UPM packages — no extra attribution required.
- `UniversalController.fbx` ships within that sample under the same license. The combined mesh asset we derive from it is a transformation of that source and inherits the same terms; storing the derived mesh in the project tree is permitted.
- Source FBX itself stays out of git (gitignored under `Assets/Samples/`), so the repo doesn't redistribute Unity's sample tree — only the derived asset committed in `Assets/Models/`.

## Untouched

Networking, voice, menu, XR rig (anchors / camera / VRRig), input feedback script, XRHeadTracker, LAN/Internet logic, tutorial panel, ForestFloor material, mountain backdrops, campfire mesh + flame + embers + FireLight + crackle audio, starfield, trees, tree wind, fire-pit kerbstones, stone seats, perimeter stones, grass tufts, bark material, controller-grip pose tracking (still grip, not palm — same known offset documented in `UnityProject/README.md`), remote-avatar hand-cube visuals (separate concern, lives in `NetworkHead.cs`).

## Next tiny polish step if needed

In rough priority order:

1. **Tune the HandController tint / smoothness** if the controllers read as too dark / too matte in headset. Inspector edit, no code changes.
2. **Mirror the right hand** (`localScale.x = -0.9`) if the joystick / trigger position feels wrong on one side. The combined mesh inherits whatever asymmetry the FBX has; quick to flip.
3. **Use the network-avatar hand cubes path** — apply the same combined mesh to whatever GameObject `NetworkHead.cs` instantiates for remote players' hands. Would unify "what I see for my own hands" with "what I see for the other player's hands" instead of the current local-controller vs. remote-cube split. Modest scope; requires a tweak to `NetworkHead.cs`.
4. **Swap to anatomical hands later** — install `com.unity.xr.hands`, import the Hands Interaction Demo sample, point `HandVisualsSetup` at the left/right hand FBX from that pack, accept the SkinnedMeshRenderer overhead. Worth doing only if/when hand-tracking actually becomes part of the experience; static-pose skinned hands without tracking tend to read worse than a clean controller.

All optional. Current state is the smallest fix that gives the hands a recognisable, intentional shape.
