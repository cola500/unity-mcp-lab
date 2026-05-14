# Roadmap

Each item below is a single slice — one commit, one observable change.

## Done

- **MCP scene manipulation** — Claude reads/edits scenes through `mcp-unity`.
- **CampfireRoom prototype** — ground, logs, flame, two seats, two player slots, camera.
- **Fire light flicker** — Perlin noise on `FireLight.intensity`.
- **Night atmosphere** — flat dark-blue ambient, skybox cleared, dim cool directional.
- **Social presence placeholders** — `FaceTarget` rotates capsules to look at the fire.
- **Quest 3 standalone build** — Oculus XR Plugin, IL2CPP/ARM64/Linear, re-runnable setup menu.
- **Seated spawn + camera offset** — explicit `CameraOffset y=1.2` under `VRRig`.
- **HMD pose tracking** — `XRHeadTracker` on `VRCamera` via `UnityEngine.XR.InputDevices`.
- **World static, only camera moves** — explicit `World` root, no environment under `VRRig`.
- **Hand/controller presence** — primitive placeholders tracking `XRNode.LeftHand` / `RightHand`.
- **Trigger feedback** — subtle scale pulse via `XRControllerInputFeedback`.
- **Minimal LAN multiplayer spike** — NGO + UnityTransport, owner-authoritative head pose sync.

## Next

- **Place remote player at PlayerSlot_B** — first `PlayerHead` snaps to the empty seat; hide the static placeholder when occupied.
- **Sync hand anchors** — same pattern as head, two more `NetworkObject`s per player.
- **Voice chat** — Vivox or a peer-to-peer alternative, spatialised.
- **Cozy polish** — bloom on the flame, ambient crackle `AudioSource`, dimmer global ambient.
- **Avatar experiments** — replace capsules + cubes with low-poly shapes (a body silhouette, simple hand mitts).
- **Interaction objects** — a stick to poke the fire, a stone you can pick up. Nothing more.

## Later

- **Internet multiplayer** — relay/STUN so it works outside LAN.
- **Persistence** — name your seat, remember the last person who sat there.
- **Shared activities** — sketching in the air, listening to a track together.
- **Spatial audio polish** — fire crackle in 3D, voice attenuation by distance to fire.

## Explicit non-goals

- Locomotion.
- Combat / game systems.
- Realistic graphics.
- Cross-platform beyond Quest standalone in the medium term.
