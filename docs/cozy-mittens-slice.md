# Cozy mittens slice

> Procedural low-poly mittens replace the XRI UniversalController mesh on the local hands. ~50% fewer triangles, fits the campfire's warm low-poly aesthetic, zero new packages. The controller mesh stays on disk as a one-menu-click fallback.

## Why mittens

From the controller-visuals audit (`docs/controller-visuals-audit.md`): the local hands had been wearing a generic Quest-Touch-family silhouette since the hand-visuals slice — readable in headset, but dated and visually flat (no texture, no normal map, single matte colour). All the "make it actually look better" options came with tradeoffs:

- **Install `com.unity.xr.hands` + samples** — realistic hands, but fingers default to T-pose without hand-tracking data + adds ~18 MB.
- **Install Meta XR Core SDK** — accurate Touch 3 / Touch Pro models, but hundreds of MB.
- **Switch XR backend to OpenXR** — runtime-loaded controller models, but architectural change for a polish issue.
- **CC0 low-poly hand mesh import** — need to find one, license-check, UV-rework.
- **Procedural mittens** — fits the existing cozy stylized direction (dog, dirt ground, stone seats, bark-textured wood pile), no new dependencies.

The audit recommended mittens, and this slice executes that.

## What landed

| New asset | Path | Notes |
|---|---|---|
| `MittenHandsSetup` Editor helper | `UnityProject/Assets/Editor/Environment/MittenHandsSetup.cs` | Menu: `Tools/Quest Setup/Apply Mitten Hands`. Idempotent. |
| `LeftMittenHand.asset` | `UnityProject/Assets/Models/LeftMittenHand.asset` | ~215 KB, ~2,384 triangles. Procedurally generated; baked once and saved as a Mesh asset. |
| `RightMittenHand.asset` | `UnityProject/Assets/Models/RightMittenHand.asset` | Same as left, with the thumb mirrored to the `-X` side. |
| `MittenWarm.mat` | `UnityProject/Assets/Materials/MittenWarm.mat` | Standard shader (Built-In RP). Warm wool tint `(0.42, 0.28, 0.20)`, metallic 0, smoothness 0.04 — reads as fabric in firelight, not plastic. |
| Scene edit | `Assets/Scenes/CampfireRoom.unity` | `LeftHandMesh` + `RightHandMesh` MeshFilter/MeshRenderer swap to point at the new mesh + material. localScale (0.9, 0.9, 0.9) and the +45° X-pitch from `vr-alignment-polish` are preserved. |

## Mesh construction

Each mitten is the union of four primitive sub-meshes baked via `Mesh.CombineMeshes`:

```
Frame (authored before the hand-anchor pitch is applied):
  +Z = pointing direction (where the controller "points")
  +Y = back of hand (up when relaxed)
  +X = hand's local right

Palm sphere         pos (0, 0, 0)              scale (0.060, 0.045, 0.075)
  Slightly egg-shaped; flattened on Y so the back-of-hand silhouette
  reads as a hand, not a baseball.

Finger bulge sphere pos (0, -0.005, 0.075)     scale (0.052, 0.042, 0.085)
  Extends forward of the palm. Centered slightly low so the silhouette
  reads as "knuckles ahead" rather than a smooth blob.

Thumb sphere        pos (±0.045, 0.005, 0.020) scale (0.030, 0.028, 0.045)
                    rot (15°, ±30°, ∓8°)
  Sphere (not capsule) for the rounded mitten thumb compartment.
  Position + rotation flips sign with `thumbSide` — left hand's thumb
  is on +X (inward toward body center when controller is held naturally),
  right hand's is on -X (also inward). Headset-validated; see "Thumb
  direction — what actually reads right" below.

Cuff cylinder       pos (0, 0, -0.050)         scale (0.078, 0.014, 0.078)
                    rot (90°, 0°, 0°)
  Thin disc at the wrist. Reads as the band where the mitten meets
  your sleeve. Single material, so no contrasting colour — just the
  silhouette change.
```

Authored size: roughly 8 × 5 × 16 cm before the 0.9× scale on the hand mesh GameObject. With scale applied → ~7 × 4 × 14 cm visible in headset. Slightly oversized compared to the controller mesh (intentional — mittens are bulkier than bare controllers, that's the whole point).

## Materials / palette

Single shared material `MittenWarm.mat` across both hands. Standard shader, matte fabric tone:

| Property | Value | Why |
|---|---|---|
| `_Color` | `(0.42, 0.28, 0.20, 1)` | Sun-bleached wool brown. Lives between the dog coat `(0.55, 0.38, 0.22)` and the dark grey controller `(0.22, 0.20, 0.18)` we replaced. |
| `_Metallic` | `0` | Fabric, not metal. |
| `_Glossiness` | `0.04` | Almost zero specular — fabric should swallow the fire's flicker, not bounce it. |
| `_MainTex`, `_BumpMap`, etc. | none | Solid colour, single material, single draw call. |

The wool tone sits in the same family as the campfire wood pile (`CampfireWood.mat`), the dog (`DogCoat.mat`), and the remote head (`RemotePlayer.mat`). The intent: when you look at your own hands from across the fire, they belong to the scene's colour world.

## Performance

| Metric | Before (controller mesh) | After (mittens) |
|---|---|---|
| Triangles per hand | ~5,000 | ~2,384 |
| Material | `HandController.mat` (Standard, solid) | `MittenWarm.mat` (Standard, solid) |
| Submeshes | 1 (combined) | 1 (combined) |
| Textures | 0 | 0 |
| Shadow casters | 0 (off) | 0 (off) |
| Draw calls | 1 per hand | 1 per hand (could batch with remote hands later) |
| BoxCollider | yes (inert) | yes (inert, untouched) |

Net runtime impact: slightly cheaper than before. Both meshes load from `.asset` files, no per-frame procedural cost.

## Fallback path

The previous `HandsControllerMesh.asset` (703 KB) and `HandController.mat` are intentionally **left on disk**. Two menu items now exist for hand-visual swapping:

- `Tools/Quest Setup/Apply Mitten Hands` — current default. Assigns mitten mesh + `MittenWarm.mat`.
- `Tools/Quest Setup/Apply Hand Visuals` — restores the combined XRI UniversalController + `HandController.mat`. Useful for A/B comparison in headset, or if mittens read wrong.
- `Tools/Quest Setup/Apply Hand Visuals (Force Sphere)` — older sphere-fallback. Lowest-fidelity option.

Each menu is idempotent and overwrites the previous selection's MeshFilter / MeshRenderer assignments. No mesh assets are deleted by any of them.

## XR / input / networking impact

Zero. By design:

- `LeftHandAnchor` / `RightHandAnchor` transforms are untouched. `XRHeadTracker` continues to drive them from `InputDevices.GetDeviceAtXRNode(XRNode.LeftHand/RightHand)`.
- `XRControllerInputFeedback` continues to auto-bind to `transform.GetChild(0)` (= `LeftHandMesh`/`RightHandMesh`) at OnEnable. `_baseScale` re-captures `(0.9, 0.9, 0.9)` cleanly on next play — the trigger-press 15 % scale pulse continues to fire on the mitten mesh.
- `BoxCollider` retained — no script depends on it, but no reason to remove it in this slice.
- `NetworkHead.cs` and `PlayerHead.prefab` (= the remote avatar) are **not** touched. Remote players still see the XRI controller mesh + `HandController.mat` on each other's hands. See "Remote avatars" below.

## Remote avatars — recommendation, not action

Per slice spec ("Do NOT replace remote avatar visuals yet unless trivially shareable") I deliberately left `PlayerHead.prefab` on the controller mesh. The mitten meshes are local-only for this slice.

If the headset test reads the local mittens as a clear win, the trivial-share extension is a 5-line addition to `MittenHandsSetup.Apply()` that also loads `PlayerHead.prefab` via `AssetDatabase.LoadAssetAtPath<GameObject>`, finds the `LeftHandVisual` / `RightHandVisual` children, swaps the same mesh + material via the existing `RemoteAvatarPolish.cs` pattern, and saves the prefab. The rationale for waiting is just "validate the look locally first, then mirror it".

## What was NOT changed

- `NetworkBootstrap`, `NetworkHead`, `ClientNetworkTransform`, `VoiceBootstrap`, `ServicesBootstrap` — networking + voice untouched.
- `XRHeadTracker`, `XRControllerInputFeedback`, `XRTrackingOriginSetter` — tracking untouched.
- `LeftHandAnchor`, `RightHandAnchor`, `VRRig`, `CameraOffset` transforms — XR rig untouched.
- `HandVisualsSetup.cs` (the older controller-mesh helper) — kept as the fallback menu.
- `PlayerHead.prefab` — remote avatar untouched.
- Scene hierarchy beyond the two MeshFilter/MeshRenderer property edits.
- Room code, session logic, build pipeline, all input bindings.

## Validation

- `recompile_scripts` after adding `MittenHandsSetup.cs`: **0 errors, 34 warnings** (all pre-existing third-party CS0618 deprecations).
- `Tools/Quest Setup/Apply Mitten Hands` ran cleanly: `[MittenHandsSetup] Applied cozy mittens to 2 hand mesh(es) — material=MittenWarm (RGBA(0.420, 0.280, 0.200, 1.000)), tris≈2384 per hand.`
- Scene post-save: `LeftMittenHand` (1 ref), `RightMittenHand` (1 ref), `MittenWarm.mat` (2 refs — both hands). XRHeadTracker (3 refs) and XRControllerInputFeedback (2 refs) intact.
- Old `HandsControllerMesh.asset` + `HandController.mat` still on disk for fallback.
- Console clean after the helper run.

## Thumb direction — what actually reads right

Headset-validated. The original `thumbSide` mapping (`+1` for left, `-1` for right → thumbs on the **inner** side of each mitten, pointing toward the body's centerline) is the one that reads correctly in VR.

An interim attempt swapped the signs based on a misreading of the user's first bug report ("left mitten on right hand and vice versa"). The hypothesis was that the Quest grip-pose `+X` convention put thumbs on the wrong axis, so flipping `thumbSide` would correct the mirror. Headset test of that flipped state rejected it explicitly: with thumbs now sticking **outward** (away from the body), the mittens read as wrong-handed. Reverted to the original `+1`/`-1` mapping.

The lesson: don't reason about Quest controller axis conventions from first principles. The grip-pose `+X` direction relative to "what the user perceives as their thumb side" depends on Unity's XR backend, the controller model, *and* the -45° X pitch later applied to the mesh — they compose. The only reliable validation is putting it on and looking.

What was NOT touched during either attempt or the revert:
- `LeftHandAnchor` / `RightHandAnchor` transforms or `XRHeadTracker.node` values
- `XRControllerInputFeedback` (still auto-binds to `transform.GetChild(0)`)
- `LeftHandMesh` / `RightHandMesh` GameObject parenting, scale, or rotation
- `MittenWarm.mat`, `HandsControllerMesh.asset`, `HandController.mat`, or any fallback path
- `PlayerHead.prefab` (remote avatars still on the controller-mesh look)

If a future tester reports the mittens read wrong again, **before changing `thumbSide`**: check whether the `XRHeadTracker.node` field on each anchor is still `LeftHand`/`RightHand`, that scene parenting is unchanged, and that the -45° X pitch on each mesh (see "Final headset-validated grip alignment" below) hasn't drifted to zero. Only after all three are confirmed should `thumbSide` come back into question.

## Grip pose alignment polish

Headset test of the procedurally-generated mittens revealed a separate alignment issue from the thumb-direction question: the mittens read as **pistols aiming forward**. The mesh's local +Z (documented in the authoring frame as "pointing direction") was aligning with the tracked controller's pointer ray and rendering the mittens like rigid weapons protruding from the wrists rather than relaxed hands wrapped around a controller.

The Quest Touch controller is a vertical-grip device — when held naturally with your arm at rest, the controller body extends roughly upward from your fist. The device pose Unity returns (via `InputDevices.GetDeviceAtXRNode`) tracks this orientation directly, so a mesh authored with its local +Z aligned to "controller pointer direction" extends along the controller's long axis. Without a wrist-flex offset, the mesh extends the controller's geometry forward rather than wrapping around it.

The fix is a local-rotation offset on the visual children only — the XR anchors that drive tracking are left untouched. See "Final headset-validated grip alignment" below for the values that landed.

### Attempts that were rejected in headset

1. **`(0°, 0°, 0°)`** — no offset. The original "pistol aiming forward" state that triggered the slice.
2. **`(+45°, ±8°, 0°)`** — pitching *forward* (toward where the palm faces). The intuition was "fingers curl down toward the lap"; in headset this folded the mittens into / under the controller body so they read as broken wrists rather than relaxed hands.
3. **Final: `(-45°, ±8°, 0°)`** — pitching *backward* (away from the palm) instead, so the mittens curl back over the knuckles. Headset-validated.

The two-attempt path matters because it shows the wrist-flex direction was the non-obvious bit, not the magnitude. ±45° was the right scale either way; only the sign needed the headset to disambiguate. Documented here so a future polish iteration doesn't re-derive the wrong direction.

### Why this is NOT a hand-assignment fix

The XR wiring (anchor → `XRHeadTracker.node`, scene parenting, mesh asset assignment routed by GameObject name) is structurally correct and was validated when the [thumb direction question](#thumb-direction--what-actually-reads-right) was resolved. This polish is purely a **visual offset on the mesh transform** — applied to the same `LeftHandMesh` / `RightHandMesh` GameObjects, not to anchors, not to mesh assets, not to `thumbSide`, not to anchor `XRNode` selection.

### What was NOT touched

- `LeftHandAnchor` / `RightHandAnchor` transforms or `XRHeadTracker.node` values
- Mitten mesh geometry (`LeftMittenHand.asset` / `RightMittenHand.asset` byte-identical to pre-polish)
- `MittenWarm.mat`, `HandsControllerMesh.asset`, `HandController.mat`, or any fallback path
- `MittenHandsSetup.cs` (mesh-generation logic, including the headset-validated `thumbSide` mapping)
- `XRControllerInputFeedback` (trigger-press scale pulse continues to operate)
- `PlayerHead.prefab` (remote avatars still on the controller-mesh look, no rotation offset baked in)
- Networking, voice, session, room, or input bindings

## Final headset-validated grip alignment

These are the live values in `Assets/Scenes/CampfireRoom.unity` after iterative headset tuning. Source of truth for any future regression check.

| Field | `LeftHandMesh` | `RightHandMesh` |
|---|---|---|
| `localPosition` | `(0, 0, 0)` | `(0, 0, 0)` |
| `localEulerAngles` | `(-45°, +8°, 0°)` | `(-45°, -8°, 0°)` |
| `localScale` | `(0.9, 0.9, 0.9)` | `(0.9, 0.9, 0.9)` |
| `localRotation` (quat) | `(-0.3817512, 0.06444657, 0.026694642, 0.921629)` | `(-0.3817512, -0.06444657, -0.026694642, 0.921629)` |

Stored representation in the scene YAML uses Unity's `0–360°` form: `(315°, 8°, 0°)` for left and `(315°, 352°, 0°)` for right — numerically identical to the signed values above.

### Why this reads as natural in headset

- **-45° X pitch** rotates the mesh's local +Z (mesh forward) **back over the knuckles** rather than forward along the controller's pointer. With the Quest Touch held grip-up at lap height, this places the mitten so it appears to wrap around and over the user's fist — the silhouette reads as a hand cupping the controller from behind, not a rigid extension of it.
- **±8° Y yaw inward** (positive for left, negative for right) angles each hand subtly toward the body's midline / the campfire. Matches how arms naturally fall when seated and looking down at a shared focal point. The magnitude is small enough that the hands don't visibly cross or converge — it just removes the "shoulder-wide aimed-outward" stiffness of zero yaw.
- **No `localPosition` offset** — the wrist pivot stays at the tracked controller origin so the mittens move 1:1 with hand motion without any positional lag or floating-wrist artefacts.
- **`localScale (0.9, 0.9, 0.9)` preserved** from earlier slices — slightly under-sized mittens that don't crowd the camera when hands come close to the face.

### Why "pistol forward" was rejected

The original `(0°, 0°, 0°)` state had the mesh's local +Z aligned with the tracked controller's pointer ray. In headset this looked like the mittens were rigid extensions of the controller body, pointing wherever the user aimed — the visual broke the "soft cozy hands" target hard. The intermediate `+45°` attempt folded the mittens down into the controller body (broken-wrist read). Only the -45° backward-curl placed the mesh behind the controller in a way that registers as a hand wrapping a grip.

If a future build regresses on this — e.g. the rotation drifts back to zero through a scene-merge accident, or the mesh's authoring frame in `MittenHandsSetup` changes — the symptom in headset will be the same forward-pistol pose. Restore `localEulerAngles = (-45°, ±8°, 0°)` on both meshes; values above are exact.

## Headset validation still needed

- **Silhouette proportions** — palm/finger-bulge/thumb sizes are tuned from the controller-mesh footprint (~10 cm). May read too small or too bulky in headset.
- **Cuff visibility** — the wrist disc was sized to match the back-of-hand width; if the camera-to-hand distance feels different in VR, the cuff might disappear behind the palm or stick out as a brim.
- **Thumb angle** — the `(15°, ±30°, ∓8°)` rotation is a first guess. Real mittens have the thumb sticking out *and* slightly forward; if it reads as "boxing glove with a tumour" the angle's wrong.
- **Colour in firelight** — `(0.42, 0.28, 0.20)` should warm up under FireLight's tint, but the actual readability in VR is unknown until headset test.
- **Trigger-press pulse** — the 15 % scale animation on `_baseScale * 1.15f` should still feel correct on the mitten mesh.

All tunable via constants at the top of `MittenHandsSetup.cs`. Re-run the menu after any edit; it's idempotent.

## Future options

In rough order of polish-value-per-effort:

1. **Mirror to remote avatars** — 5-line addition to apply same mesh + material to `PlayerHead.prefab` so friends see each other in matching mittens.
2. **Two-toned mittens** — split palm + finger bulge into separate sub-meshes, assign a slightly lighter wool tone to the palm via a second material slot. Reads as "darker on top, lighter on the gripping side" without needing a texture.
3. **Cuff colour stripe** — extract the cuff cylinder into its own submesh, give it a third material in a contrasting warm tone (deep red, forest green, mustard). Tiny ornament that picks a "team colour" per player if we ever do multiple pairs of testers.
4. **Subtle wool texture** — CC0 noise-pattern albedo + normal map (Polyhaven has a few "fabric" textures suitable). Adds detail without per-vertex authoring. ~10 lines added to material setup.
5. **Animated thumb on trigger press** — track trigger axis (already polled in `XRControllerInputFeedback`), animate the thumb sphere's rotation by ~10° on press for the "I'm pinching" feedback. Tiny scope, looks alive.
6. **Per-finger split** — abandon the mitten silhouette for full gloves with five separate fingers. Big jump in complexity (especially without bones/animation); not recommended for an indie cozy project.
