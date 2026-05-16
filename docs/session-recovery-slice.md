# Session recovery slice — guard + in-VR Stop

> Two-part UX safety pass after the 2026-05-16 fika test surfaced a stuck-session loop. Adds a guard on the join button and a long-press Y as an in-headset Stop. No networking, voice, scene, or visual logic touched beyond what the original `Stop()` already did.

## What the headset test showed

Pulled the debug log after the session (see [debug-logging.md](debug-logging.md) for how). The 89-event timeline made the failure mode obvious:

1. User hosted room A successfully (`relay_host_attempt` → `relay_alloc_succeeded` → `voice_joined` → `relay_host_ready` at 73 s).
2. User cycled to room B and pressed **B** to join. `relay_join_attempt` immediately threw:
   ```
   [ServicesBootstrap] JoinRelay: SessionException:
     [Error: SessionConflict] [Message: player is already a member of the lobby]
   ```
3. From there the app was effectively stuck — the user couldn't recover without quitting. They tried LAN host/join (`StartHost-returned-false` × 6 because NGO was still running), toggled mode 7 more times, hit A (recenter) twice in confusion, eventually managed to host again on room C through a code path that probably should've failed (the prior session wasn't fully cleaned up).

Root causes:

- **Pressing B while already hosting** is never useful — you're already in the lobby you're trying to join. Unity Services Multiplayer raises `SessionConflict` and the user has no way to know that B is the wrong button for them.
- **There was no in-VR way to call `Stop()`.** The `Stop()` method existed but was only bound to the Editor's `X` key, useless in headset. The user had to power-cycle by quitting from the Meta system menu.

## What this slice changes

### 1. Guard — `OnRightSecondary` ignores join while hosting

```csharp
void OnRightSecondary()  // B button
{
    var nm = NetworkManager.Singleton;
    if (nm != null && nm.IsHost)
    {
        _state = $"Already hosting Room {CurrentLetter}";
        DebugLogger.Log("join_ignored_already_hosting", null, ("room", CurrentLetter.ToString()));
        return;
    }
    if (mode == Mode.Relay && _services != null && _services.InRelaySession)
    {
        _state = $"Already in Room {CurrentLetter}";
        DebugLogger.Log("join_ignored_already_in_session", null, ("room", CurrentLetter.ToString()));
        return;
    }
    // ... existing StartClient path ...
}
```

Effect: pressing B while hosting writes one `join_ignored_already_hosting` log entry and updates the panel's notification line to "Already hosting Room X" for ~5 s (the existing `TutorialOverlay` notification window). No network call. No `SessionConflict`. No console spam — one event per press, not per frame.

### 2. In-VR Stop — long-press left Y for 1.5 s

Y on the left controller used to be a press-edge toggle for `Mode.Lan ↔ Mode.Relay`. Now Y has two behaviours depending on how long it's held:

| Press duration | Action |
|---|---|
| Short (release before 1.5 s) | ToggleMode (unchanged from prior behaviour) |
| ≥ 1.5 s | `Stop()` — leave voice room, leave Relay session, shutdown NGO. Then reset local UI state. |

Implementation detail: ToggleMode is now fired on **release-edge** of Y, not press-edge, and is suppressed if a long-press already claimed the press. So short tap = same feel as before (mode toggles when you let go, ~100 ms latency, imperceptible). Hold = clean Stop.

The polling for Y is moved out of the generic `PollController` flow into a dedicated `PollYLongPress()` method. `PollController` is now called with `null` for the LeftHand secondary callback.

### 3. Hardened `Stop()`

`Stop()` already existed but was minimal. The new version wraps each teardown step (voice / Relay / NGO) in a try/catch so a partial failure doesn't block the rest, and the user always reaches the idle state:

```csharp
async void Stop()
{
    bool clean = true;
    try { _voiceBootstrap?.LeaveRoom(); }
    catch (Exception e) { clean = false; DebugLogger.Log("stop_step_failed", "voice_leave", ("error", e.Message)); }

    if (_services != null && _services.InRelaySession)
    {
        try { await _services.LeaveRelayAsync(); }
        catch (Exception e) { clean = false; DebugLogger.Log("stop_step_failed", "relay_leave", ("error", e.Message)); }
    }

    var nm = NetworkManager.Singleton;
    if (nm != null && (nm.IsHost || nm.IsClient))
    {
        try { nm.Shutdown(); }
        catch (Exception e) { clean = false; DebugLogger.Log("stop_step_failed", "ngo_shutdown", ("error", e.Message)); }
    }

    _joinCodeInput = "";
    _hostedAlias = "";
    _busy = false;

    _state = "Stopped session";
    DebugLogger.Log(clean ? "stop_completed" : "stop_completed_with_errors", null,
        ("mode", mode.ToString()), ("room", CurrentLetter.ToString()));
}
```

Preserved across the stop:

- `mode` (LAN or Relay choice)
- `_codeChars[0]` (room letter)
- All scene state (campfire, dog, environment, hands)
- Voice cloud connection (only the **room** is left, not the Photon master connection itself — voice can re-join a different room cleanly on next host/join)

## Updated event vocabulary

| Event | When | Replaces |
|---|---|---|
| `stop_requested` | User triggers Stop (long-press Y or Editor X) | (was `stop_pressed`) |
| `stop_step_failed` | A teardown step (voice/relay/ngo) threw | new |
| `stop_completed` | All teardown steps succeeded | (was `stopped`) |
| `stop_completed_with_errors` | At least one step failed but state was reset | new |
| `join_ignored_already_hosting` | User pressed B while NGO `IsHost` | new |
| `join_ignored_already_in_session` | User pressed B in Relay mode while still in a Relay session | new |

See [debug-logging.md](debug-logging.md) for the full event catalogue.

## Manual test sequence (per spec)

To verify in Editor or headset:

1. **Host A** — press X. Expect `relay_host_ready` in log.
2. **Press B while hosting** — expect:
   - No `relay_join_attempt` (the guard fires first)
   - One `join_ignored_already_hosting` event
   - Panel notification: "Already hosting Room A"
   - No `SessionConflict` exception
3. **Long-press left Y for ~1.5 s** — expect:
   - One `stop_requested` event with `msg: "Y long-press"`
   - One `stop_completed` (or `stop_completed_with_errors` if voice was mid-handshake)
   - Panel notification: "Stopped session"
   - `_state` resets, room letter + mode preserved
   - **No** subsequent ToggleMode event (the release-edge ToggleMode is suppressed because the long-press claimed the press)
4. **Host A again** — press X. Should work first try (no leftover Relay session).
5. **Join A from another headset** — should connect normally.

Short tap of Y (< 1.5 s) should still toggle mode exactly as before.

## What was NOT changed

- `Stop()` teardown order — same as before (voice → Relay → NGO).
- `StartHost`, `StartClient`, `ToggleMode`, `Recenter`, `CycleLetter`, `UpdateStickCycle` — untouched.
- NetworkHead, VoiceBootstrap, ServicesBootstrap, ClientNetworkTransform — untouched.
- Scene, prefabs, materials — untouched (this is a script-only slice).
- Editor `X` key still calls `Stop()` directly — Editor flow unchanged.
- `OnLeftPrimary` (X = host), `OnRightPrimary` (A = recenter), `OnRightSecondary` apart from the new guard — same.

## Known limitations

- **Long-press Y is 1.5 s.** If that feels too long in headset, drop the `StopLongPressDuration` constant to e.g. 1.2 s. If users accidentally trigger Stop during mode toggle, raise to 2.0 s. One-line edit.
- **No visual countdown.** Holding Y gives no "you've held for X seconds" feedback. Users have to trust the duration. A small ring-fill indicator on the panel during hold would be polish for a later slice.
- **The guard only checks `NetworkManager.IsHost` and `_services.InRelaySession`.** If you're a *client* successfully connected (`IsClient && !IsHost`), pressing B again would still call `StartClient` and probably throw — but that's a separate edge case and the original log didn't surface it. Worth revisiting if it shows up.
- **No equivalent guard on X (host while already hosting).** Hitting X twice in a row currently re-runs `StartHost` and creates a second Relay allocation, leaving the first orphaned. Out of scope for this slice; add an `IsHost`-guard in `StartHost` if it becomes a problem.

## What still needs headset validation

1. Long-press feels right (1.5 s is a good middle, but check).
2. ToggleMode-on-release feels indistinguishable from ToggleMode-on-press for normal short taps.
3. After Stop, hosting works on the same room letter without re-toggling mode.
4. The "Already hosting Room X" notification reads clearly in firelight.
5. Stop after a stuck `SessionConflict` actually un-sticks NGO + Unity Services (the prior session's lobby membership should drop on Stop).

## Validation

- `recompile_scripts` after the edits to `NetworkBootstrap.cs`: **0 errors, 0 warnings.**
- File-level diff stat: `NetworkBootstrap.cs +117 / -7` (combined: long-press polling + Y refactor + B guard + hardened Stop).
- No other source files touched. No scene, no prefab, no material, no doc beyond this file and the two tagged-as-fixed lines in `app-alignment-qa.md` + `remote-fika-test-debug-checklist.md`.
