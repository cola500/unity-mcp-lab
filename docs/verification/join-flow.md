# Visual verification — single-letter room join flow

> Slice: pre-headset visual check of the single-letter (A–Z) room code UX. Four reproducible UI states can be snapped to PNG from the Unity Editor's Play Mode via `Tools/Verification`. Connected state requires a real two-Quest session and is left for the headset pass.

## What we want to verify

| # | Property | How |
|---|---|---|
| 1 | Room is exactly one letter | "Room: X" line shows a single character, no slots/brackets |
| 2 | Allowed letters are A–Z | Cycle wraps through all 26 letters |
| 3 | Default is A | Fresh launch reads "Room: A" without any user input |
| 4 | Host alias matches selected letter | After change-to-D + host, "YOUR FIRE / Room: D" is shown |
| 5 | Joiner reads selected letter | After change-to-D + join, "Joining room D…" is shown |
| 6 | No 6-character Relay code is exposed | The underlying Unity Relay token is never rendered |
| 7 | Quest-facing legend mentions Quest buttons only | "X host room", "B join room", "Y mode", "A recenter", "stick change room" |
| 8 | Editor-only debug strip is gated | "(editor keys: H host, C join, M mode, X stop)" only present in Editor |

## How to capture the screenshots

A re-runnable Editor tool drives `NetworkBootstrap` into each UI state via reflection so we can capture without a live network.

1. Open `Assets/Scenes/CampfireRoom.unity`.
2. Press **Play** in the Unity Editor.
3. Click in the Game view so it has input focus.
4. Run each menu item in turn:
   - `Tools → Verification → Capture 1 — Idle Relay (Room A)`
   - `Tools → Verification → Capture 2 — Hosting Room A`
   - `Tools → Verification → Capture 3 — Idle after cycling to Room D`
   - `Tools → Verification → Capture 4 — Joining Room D`

Each menu writes a PNG to `docs/verification/join-flow/`. Filenames:
```
01-idle-room-a.png
02-hosting-room-a.png
03-idle-room-d.png
04-joining-room-d.png
```

Stop Play Mode after the four captures. The reflected state is discarded automatically; the saved scene is untouched.

If you press the menu while not in Play Mode, the tool refuses with `[Verification] Enter Play Mode first, then re-run the capture menu.` — that's the safety check, not a bug.

## Expected layout per state

The world-space `TutorialPanel` (TextMesh) hangs in front of `Camera.main`. ASCII approximations below.

### State 1 — Idle Relay, default Room A  (`01-idle-room-a.png`)

```
Mode: Relay
Local IPs: 192.168.x.x    (editor keys: H host, C join, M mode, X stop)

                          ROOM
                            A          ← warm, pulsing

                       (no state notification)

  🔥  CAMPFIRE
  Room: A

  X       host room A
  B       join room A
  Y       mode
  A       recenter
  stick   change room

  mode · Internet
```

Verify:
- ✓ "Room: A" line under the headline
- ✓ Legend shows X/B with "room A" suffix (current letter)
- ✓ Five rows in the legend including stick line
- ✓ "mode · Internet" at the bottom
- ✓ No slot-bracket display anywhere

### State 2 — Hosting Room A  (`02-hosting-room-a.png`)

```
Mode: Relay
Local IPs: …

                          ROOM
                            A          ← warm, pulsing

                   Waiting for friend

  🔥  YOUR FIRE

  Room: A

  waiting for friend . . .
```

Verify:
- ✓ Headline `YOUR FIRE`
- ✓ "Room: A" mid-panel
- ✓ Spinner-suffixed "waiting for friend"
- ✓ No legend (hosting hides the legend so the room letter is the focus)
- ✓ No 6-character Relay code anywhere

### State 3 — Idle, user cycled letter to D  (`03-idle-room-d.png`)

```
Mode: Relay
Local IPs: …                              (Editor room override: D)

                          ROOM
                            D          ← warm, pulsing

                          Room D

  🔥  CAMPFIRE
  Room: D

  X       host room D
  B       join room D
  Y       mode
  A       recenter
  stick   change room

  mode · Internet
```

Verify:
- ✓ All "A" references replaced with "D" — letter is purely substituted
- ✓ "Room D" notification (transient, fades after 5 s)
- ✓ Layout otherwise identical to State 1

### State 4 — Joining Room D  (`04-joining-room-d.png`)

```
Mode: Relay
Local IPs: …

                         (no ROOM block; only host shows the alias)

                   Looking for room D . . .

  🔥  CAMPFIRE

  Looking for room D . . .
```

Verify:
- ✓ Headline `CAMPFIRE`
- ✓ "Looking for room D" with spinner
- ✓ The joiner sees the letter they're trying to reach
- ✓ No 6-character Relay token shown

## What still requires Quest headset testing

These cannot be verified from the flat Editor view because they involve VR rendering, live network state, or actual controller input:

1. **Stick cycles the letter on real hardware.** Editor reflection bypasses the input layer; only a real thumbstick can validate the deadzone / repeat feel.
2. **Auto-repeat feels right** when holding the stick — needs a real hand.
3. **Connected / "Friend joined the fire" state** — needs a live second Quest, real Photon Voice room, real Relay session.
4. **Spatial voice direction** — flat Game view has no stereo HRTF.
5. **Tutorial panel billboarding smoothness** — the Slerp toward `Camera.main` is only visible when the camera actually rotates (i.e., in headset).
6. **Photon room property handoff** — the `rc` (real Relay code) custom property only exists in a real running Photon room.
7. **Collision behaviour** when two hosts happen to pick the same letter — needs two concurrent sessions on the same Photon AppId. With A–Z, there are 26 possible rooms; defaulting to A means any two users who don't change letter will collide.

Items 1–5 are part of the **next headset session** (use [remote-fika-test.md](../remote-fika-test.md) as the protocol). Items 6–7 are noted in [voice-research.md](../voice-research.md) for awareness.

## Notes for future captures

- The capture tool uses reflection to set `_codeChars[0]`, `_hostedAlias`, `_joinCodeInput`, `_state`, etc. on `NetworkBootstrap`. If those fields are renamed, update `VerificationCapture.cs` accordingly.
- Screenshots use the live `Camera.main` (`VRCamera`). In Editor Play Mode it renders monoscopic; in headset it would be the right eye. Both acceptable for layout verification.
- The four states cover the *new* behaviour from the single-letter room slice. Earlier captures from the 3-letter ABC era are obsolete and were removed in the same slice.
