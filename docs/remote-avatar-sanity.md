# Remote avatar — sanity audit + polish

> What each player actually sees of the other player in CampfireVR multiplayer, what's networked, and what tiny visual fix landed in this slice.

## Current implementation

### The pieces

- **`Assets/Prefabs/PlayerHead.prefab`** — the spawned avatar. NGO instantiates one per connected player. Hierarchy:
  ```
  PlayerHead/                     (NetworkObject + ClientNetworkTransform + NetworkHead)
    HeadVisual/                   (sphere, 0.2 m)
    LeftHandVisual/               (cube, was 0.07 × 0.04 × 0.12 m — pre-slice)
    RightHandVisual/              (cube, was 0.07 × 0.04 × 0.12 m — pre-slice)
  ```
- **`Scripts/Networking/NetworkHead.cs`** — owner-side computes the head/hand mirror, writes hand pose to NetworkVariables, head pose via `ClientNetworkTransform`.
- **`Scripts/Networking/ClientNetworkTransform.cs`** — one-liner that flips `OnIsServerAuthoritative()` to `false`. Owner-authoritative transform sync.
- **`Scene/VRRig`** — local rig the owner drives with head + hands.
- **`Scene/RemoteRig`** — a "mirror" rig placed at the opposite seat. Used by NetworkHead to translate owner-rig-relative offsets into the remote viewer's coordinate frame.
- **`Scene/PlayerSlot_B`** — static placeholder (some mesh in the second seat) shown while no remote player is connected. `NetworkHead.OnNetworkSpawn` hides this when a remote player arrives, and restores it on despawn.

### What's actually networked

| Field | How | Authority |
|---|---|---|
| `PlayerHead.transform` (position + rotation) = remote viewer's perceived head position | `ClientNetworkTransform` (NGO `NetworkTransform` with server-auth disabled) | Owner |
| Left hand pose `_leftPos`, `_leftRot` | `NetworkVariable<Vector3>` + `NetworkVariable<Quaternion>` (write = Owner, read = Everyone) | Owner |
| Right hand pose `_rightPos`, `_rightRot` | Same pattern | Owner |
| Voice audio | Photon Voice 2 (separate path; not in NetworkHead) | Owner mic → Photon → remote Speaker |
| Hands LOCAL controller visuals | Not networked — each side renders its own controller mesh from the XR rig | — |
| Hands LOCAL trigger feedback (1.15× scale on press) | Not networked — local-only feel | — |

### What each player sees

Per session of two players (Johan + friend):

**Johan's headset shows:**
- Johan's own **hands**: `LeftHandMesh` / `RightHandMesh` under `VRRig/CameraOffset/...` — the combined UniversalController mesh (XRI) with `HandController.mat`. Local-tracking, no network.
- Johan's own **head**: nothing — `Camera.main` IS Johan's head. No visual.
- Friend's **head**: a `HeadVisual` GameObject (child of friend's `PlayerHead` NGO instance) — moves as friend looks around (their owner-side syncs `PlayerHead.transform`).
- Friend's **hands**: `LeftHandVisual` / `RightHandVisual` GameObjects — move per NetworkVariable updates.
- Friend's **voice**: a `VoiceSpeaker` audio source positioned by `VoiceSpeakerPlacer` near the friend's `RemoteRig` seat, spatial audio.
- Static `PlayerSlot_B` placeholder: **hidden** while friend is connected.
- Everything else (campfire, stones, trees, dog, ground, lights, atmosphere, tutorial panel) — identical on both sides.

**Friend's headset shows:** the symmetric version. Friend's own hands locally; Johan's head + hands as remote avatar; their `PlayerSlot_B` hides while Johan is connected.

### Position math (NetworkHead.LateUpdate)

Owner computes head position relative to its own rig:

```
headOffset = camera.position - ownRig.position    (head's offset from rig origin)
remoteWorldHead = remoteRig.position + rotDiff * headOffset
remoteWorldRot  = rotDiff * camera.rotation
```

where `rotDiff = remoteRig.rotation * Inverse(ownRig.rotation)` is computed once at spawn and assumes the two rigs are mirrored across the campfire. Same math for hands, with the hand anchor transforms as sources.

Result: when Johan leans forward in his physical room, his avatar leans forward toward the fire from Friend's seat — coordinated across rigs, even though the two players might be sitting in totally different rooms.

## The "white pill" problem

The original `PlayerHead.prefab` shipped with:

- `HeadVisual`: built-in Sphere mesh, **default-material** (Unity's `fileID 10303, guid 0000000000000000f000000000000000`). White, slight specular sheen, no tint. At 0.2 m diameter it reads as a "white pill" or "ghost ball" floating across the fire.
- `LeftHandVisual` / `RightHandVisual`: built-in Cube mesh, same default white material, non-uniform scale `0.07 × 0.04 × 0.12` — the exact same rectangle the *local* hands used before the XRI controller-mesh slice. Two more white nubs floating where the friend's hands would be.

In other words: while the local player got a polish pass (warm dark-grey controller meshes on a 45° wrist tilt), the *remote* representation was still using the original placeholder geometry from before any visual work happened. Functional but jarring — the friend reads as "debug primitives" rather than "a person".

## Polish applied this slice

A re-runnable Editor helper: `Tools/Quest Setup/Polish Remote Avatar` (in `Assets/Editor/Networking/RemoteAvatarPolish.cs`).

What it changes on `PlayerHead.prefab`:

| Visual | Mesh | Material | Scale | Shadows |
|---|---|---|---|---|
| `HeadVisual` | Sphere (unchanged) | `Assets/Materials/RemotePlayer.mat` (new — warm tan-grey `(0.55, 0.45, 0.35)`, matte, smoothness 0.10) | `(0.2, 0.2, 0.2)` (unchanged) | `castShadows = Off`, `receiveShadows = false` |
| `LeftHandVisual` | `Assets/Models/HandsControllerMesh.asset` (combined XRI UniversalController — same as local hands) | `Assets/Materials/HandController.mat` (same as local hands) | `(0.9, 0.9, 0.9)` (uniform — matches local hand proportions) | `castShadows = Off`, `receiveShadows = false` |
| `RightHandVisual` | Same as left | Same as left | Same as left | Same as left |

Result: friend's head reads as a soft warm round silhouette (still abstract, but person-shaped and tinted to match firelight). Friend's hands look identical to your own — the same Quest Touch controller geometry in the same dark grey, just on the other side of the fire.

### What was *not* changed

- **NetworkHead.cs** — untouched. Same sync code, same SerializeField bindings (`visual`, `visualLeft`, `visualRight` still point at the same GameObjects).
- **ClientNetworkTransform.cs** — untouched (1-liner anyway).
- **Prefab structure** — same root + 3 visual children. No GameObjects added or removed. No renames.
- **NetworkObject component, GlobalObjectIdHash, ownership flags** — untouched. The spawn behaviour from NGO is unchanged.
- **PlayerSlot_B placeholder** — still hidden on remote spawn, restored on despawn (the helper doesn't touch placeholder logic at all).
- **VRRig, RemoteRig** — untouched. The mirror math in NetworkHead still works as before.
- **Voice path** — Photon Voice + `VoiceSpeakerPlacer` unchanged.
- **Scene** — `PlayerHead.prefab` is spawned at runtime by NGO; it's not in the scene as a static instance, so no scene file changed in this slice.

## Limitations of the new look

- **No face direction.** The head is a sphere — when the friend turns their head, you see the sphere rotate but there's no nose / eye / hair feature to read "facing me" vs "facing the fire". Adding a small face-marker (e.g. a darker spot for eyes) would be a follow-up.
- **No torso / shoulders / body.** Just head + 2 hands floating. The illusion of a person reading + the warm material reduces the floating-debris feeling, but it's still abstract.
- **No name / identity tag.** Both players' avatars look identical. Fine for one-on-one (you know who you're with), but won't scale to 3+ players.
- **Hand mesh is the local hand mesh.** Internally consistent but assumes both players are using Quest Touch controllers. If we ever add proper hand-tracking, remote hands would need a different visual.
- **No blink, no breathing, no idle motion.** When the friend is very still, their avatar looks dead-still — no subtle life. `PresenceBreath.cs` exists for the local rig; could be extended to remote avatars.
- **Voice cue isn't visualised.** When the friend talks, the head doesn't bob, the hands don't gesture. Speaker volume / VAD-based subtle scale-pulse on the head would make conversation feel more alive.

## Future avatar options (deferred slices)

Roughly in order of effort:

1. **Face marker on the head** — a small dark patch on `HeadVisual` indicating front. ~5 lines of code, prefab edit.
2. **PresenceBreath on remote avatars** — copy the existing breathing curve onto the `PlayerHead` root or `HeadVisual`. ~10 lines.
3. **Voice-driven head pulse** — read amplitude from the friend's Photon `Speaker` and tint the head warmer or pulse-scale when speaking. ~50 lines + Photon integration.
4. **Simple torso primitive** — a tapered capsule between head and a "torso anchor" computed from head height. ~30 lines; risk: looks awkward without IK.
5. **Hand-tracking pose** — replace the controller mesh with a finger-rigged hand and sync joint poses via NetworkVariables. Big slice. Spec already rules this out for now.
6. **Full Meta XR avatar SDK** — out of scope per project policy ("do not add full avatar SDK").

## Validation

- `recompile_scripts` after adding `RemoteAvatarPolish.cs`: **0 errors, 34 warnings** (all pre-existing third-party CS0618 deprecations).
- `Tools/Quest Setup/Polish Remote Avatar` ran cleanly: `[RemoteAvatarPolish] Updated 3 visual(s) on PlayerHead.prefab: head→RemotePlayer.mat (warm tan), hands→HandController.mat + combined controller mesh @ 0.9x.`
- Prefab post-run verified via GUID greps:
  - `HandsControllerMesh.asset` (guid `a746858424ca5403f9b5d776d079dcfb`) — 2 refs in prefab ✓
  - `HandController.mat` (guid `df1049396ea9d4a3fb079126b03b264e`) — 2 refs in prefab ✓
  - `RemotePlayer.mat` (guid `0491102f843ef4f748edc1feba143389`) — 1 ref in prefab ✓
- All three children's localScale verified: `HeadVisual` = `(0.2, 0.2, 0.2)`, `LeftHandVisual` / `RightHandVisual` = `(0.9, 0.9, 0.9)`.
- `NetworkHead`'s `visual` / `visualLeft` / `visualRight` SerializeField object refs are preserved (the GameObject identities under PlayerHead weren't replaced — only the renderer's mesh + material were swapped).
- Console clean post-run.

## What needs headset validation

Can't be confirmed from the Editor alone:

1. **Warm tan tint reads as "person" in firelight.** May read as too brown, too pale, or too saturated under the campfire's flicker. Adjust `RemoteHeadTint` in the helper if needed (single Color value).
2. **Combined controller mesh on remote hands actually feels like "the same controllers as me".** Could read as confusing if my own hand visual happens to be tilted differently than the friend's (rotation isn't normalized for handedness in either path). Spot-check during the next two-headset session.
3. **Hand mesh scale 0.9 at the remote position doesn't clip into stone seats.** The mirror math places hands relative to `RemoteRig`; controller-mesh extent is ~10×5×7 cm at 0.9× — should be fine but worth a glance.
4. **Head + hands stay synchronized when one side moves quickly.** ClientNetworkTransform handles head sync at NGO tick rate; hand NetworkVariables update at whatever rate LateUpdate triggers (= per frame). Both should feel smooth.
5. **PlayerSlot_B placeholder still hides cleanly** when friend connects (the helper didn't touch this — just verify it still works post-polish).
