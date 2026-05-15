# Visual verification — ABC alias join flow

> Slice: pre-headset visual check of the 3-character A/B/C join code UX. All four UI states can be reproduced in the Unity Editor's Play Mode and snapped to PNG via the `Tools/Verification` menu. The connected state requires a real two-Quest session and is left for the headset test pass.

## What we want to verify

| # | Property | How |
|---|---|---|
| 1 | Code is exactly 3 slots | Slot count in JOIN CODE line equals 3 |
| 2 | Allowed letters are A/B/C only | Tutorial reads "letters are A B C only" |
| 3 | Current slot is highlighted | Active slot wrapped in `[X]` brackets |
| 4 | No 6-character real Relay code is exposed | CAMPFIRE CODE block shows the 3-char alias, never the underlying Unity Relay code |
| 5 | Host shows spaced alias | Display is `B C A` (spaced), not `BCA` |
| 6 | Quest-facing help text mentions Quest buttons only | "PRESS X", "PRESS B", "PRESS Y", "PRESS A" — no H/C/M/X keyboard letters |
| 7 | Editor-only debug strip is gated | "(editor keys: H host, C join, M mode, X stop)" line is only present in Editor builds |

## How to capture the screenshots

A re-runnable Editor tool drives `NetworkBootstrap` into each UI state via reflection so we can capture without a live network.

1. Open `Assets/Scenes/CampfireRoom.unity`.
2. Press **Play** in the Unity Editor.
3. Click in the Game view so it has input focus.
4. Run each menu item in turn:
   - `Tools → Verification → Capture 1 — Idle Relay`
   - `Tools → Verification → Capture 2 — Hosting alias BCA`
   - `Tools → Verification → Capture 3 — Editor slot 1`
   - `Tools → Verification → Capture 4 — Editor slot 3 (B = JOIN)`

Each menu writes a PNG to `docs/verification/abc-join-flow/`. Filenames:
```
01-idle-relay.png
02-hosting-bca.png
03-editor-slot1.png
04-editor-slot3-join.png
```

Stop Play Mode after the four captures. The reflected state is discarded automatically; the saved scene is untouched.

If you press the menu while not in Play Mode, the tool refuses with `[Verification] Enter Play Mode first, then re-run the capture menu.` — that's the safety check, not a bug.

## Expected layout per state

The world-space `TutorialPanel` (TextMesh) hangs in front of `Camera.main`. In Editor it renders to the Game view; the screenshots crop to that view. ASCII approximations below — actual text wraps to whatever the camera framing shows.

### State 1 — Idle Relay  (`01-idle-relay.png`)

```
Mode: Relay
Local IPs: 192.168.x.x    (editor keys: H host, C join, M mode, X stop)

      (no CAMPFIRE CODE block; alias is empty)

                            Idle

           PRESS X TO HOST
           PRESS B TO JOIN
           PRESS Y TO SWITCH MODE
           PRESS A TO RECENTER
```

Verify:
- ✓ "Mode: Relay" at top
- ✓ Four "PRESS …" prompts visible
- ✓ Quest button letters only (X, B, Y, A) — no H/C/M references in the prompts themselves
- ✓ No big CAMPFIRE CODE block (host alias is empty)
- ✓ Editor-only debug strip "(editor keys: …)" present *because we're in Editor*

### State 2 — Hosting alias BCA  (`02-hosting-bca.png`)

```
Mode: Relay
Local IPs: …

                       CAMPFIRE CODE
                         B C A          ← warm, pulsing

                  Waiting for friend…

           PRESS X TO HOST
           PRESS B TO JOIN
           PRESS Y TO SWITCH MODE
           PRESS A TO RECENTER
```

Verify:
- ✓ Headline `CAMPFIRE CODE` visible
- ✓ Code rendered with single spaces: `B C A` (not `BCA`)
- ✓ Exactly 3 letters, all from {A, B, C}
- ✓ No 6-character string anywhere on screen (real Relay code is internal)
- ✓ State line reads "Waiting for friend…"

### State 3 — Code editor, slot 1  (`03-editor-slot1.png`)

```
Mode: Relay
Local IPs: …                              (Editor join-code field, ignored in Quest)

                            (no CAMPFIRE CODE; we're the client side)

                       Slot 1 of 3

                JOIN CODE  (letters are A B C only)
                       [A] A A

       A = NEXT LETTER     (hold to auto-cycle)
       X = PREVIOUS LETTER (hold to auto-cycle)
       B = NEXT SLOT
       Y = BACK
```

Verify:
- ✓ Exactly 3 slots displayed
- ✓ Slot 1 highlighted with `[A]` brackets
- ✓ Other slots are plain ` A `
- ✓ Tutorial mentions the A/B/C alphabet explicitly
- ✓ Hold-to-cycle hint visible on both A and X lines
- ✓ "B = NEXT SLOT" (not "B = JOIN" — we're not on the last slot)

### State 4 — Code editor, slot 3 with BCA  (`04-editor-slot3-join.png`)

```
Mode: Relay
Local IPs: …

                       Slot 3 of 3

                JOIN CODE  (letters are A B C only)
                        B  C [A]

       A = NEXT LETTER     (hold to auto-cycle)
       X = PREVIOUS LETTER (hold to auto-cycle)
       B = JOIN
       Y = BACK
```

Verify:
- ✓ Slot 3 (the last) highlighted with `[A]`
- ✓ Slots 1–2 show committed letters `B` and `C` plainly
- ✓ "B = JOIN" — the prompt switches from NEXT SLOT to JOIN on the last slot
- ✓ The slot indicator reads "Slot 3 of 3"

## What still requires Quest headset testing

These cannot be verified from the flat Editor view because they involve VR rendering, live network state, or actual controller input:

1. **Controller buttons actually fire the actions** on real hardware. Editor reflection bypasses the input layer entirely.
2. **Auto-repeat feels right** when holding A or X — only a real hand on the controller can tell.
3. **Connected / "Friend joined the fire" state** — needs a live second Quest, real Photon Voice room, real Relay session.
4. **Spatial voice direction** — flat Game view has no stereo HRTF.
5. **Tutorial panel billboarding smoothness** — the Slerp toward `Camera.main` is only visible when the camera actually rotates (i.e., in headset).
6. **Photon room property handoff** — the `rc` (real Relay code) custom property only exists in a real running Photon room. The Editor reflection state can't reproduce it.
7. **Collision behaviour** when two hosts pick the same 3-character alias — needs two concurrent sessions on the same Photon AppId.

Items 1–5 are part of the **next headset session** (use [remote-fika-test.md](../remote-fika-test.md) as the protocol). Items 6–7 are noted in [voice-research.md](../voice-research.md) for awareness.

## Notes for future captures

- The capture tool uses reflection to set `_inputState`, `_codeChars`, `_hostedAlias`, etc. on `NetworkBootstrap`. If those fields are renamed, update `VerificationCapture.cs` accordingly.
- Screenshots use the live `Camera.main` (`VRCamera`). In Editor Play Mode it renders monoscopic; in headset it would be the right eye. Both are acceptable for layout verification.
- The four states cover all the *new* behaviour from the ABC alias slice. Earlier features (fire crackle, starfield, presence breath) are out of scope here — they're verified by the fact that they don't regress when the capture tool sets state.
