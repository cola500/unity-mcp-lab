# Remote Fika — debug-aware test checklist

> Companion to [remote-fika-test.md](remote-fika-test.md). Use this for any test session where you want to be able to diagnose failures afterwards. Adds a 5-minute setup + 5-minute teardown around the existing 20-minute structured hangout.

## Before the session

### Both sides

- [ ] **Same APK version on both headsets.**
  ```sh
  adb=/Applications/Unity/Hub/Editor/6000.4.7f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
  $adb shell dumpsys package com.unitymcplab.campfireroom | grep -E "versionName|versionCode"
  ```
  Both should print the same `versionName=1.0`. If not, re-install the latest APK on the older side.

- [ ] **Both default to room A.** No need to press the stick. If you've cycled in a previous session and remember the letter, that's fine — both just need to land on the same one.

- [ ] **Both note local wall-clock time** before launching. Compare phone clocks if you're paranoid about clock drift; even 5 s of skew makes log diffing harder. Most phones are NTP-synced within ms — you're probably fine.

- [ ] **Both confirm Internet mode** (`mode · Internet` at the bottom of the panel). LAN mode is dev-only and won't work over the internet.

- [ ] **Both connect headphones** before putting the headset on. Quest speakers leak voice into the other Quest's mic.

### Optional — pre-clean previous logs

If you want to be sure you don't confuse a new failure with stale data:

```sh
$adb shell ls /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/
# Optionally:
$adb shell rm /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/*.jsonl
```

(The logger auto-rotates after 10 files anyway. Pre-cleanup is just for clarity.)

## During the session

Run through [remote-fika-test.md](remote-fika-test.md) as normal. Layer in:

- [ ] **First moment in headset** — say out loud the wall-clock time. ("It's 14:07.") This gives both logs a known synchronisation point on top of `ts`.

- [ ] **When something feels off** — say "marker now" out loud, and if you're testing from the Editor, hit the **L key** to write a `MANUAL_MARKER` event. (In-VR marker button isn't bound yet — verbal markers are the workflow until that exists.)

- [ ] **If a session gets stuck** (frozen connection, can't host/join, "Already hosting" loop) — **long-press left Y for ~1.5 seconds** to call `Stop()` cleanly. Logs will show `stop_requested → stop_completed`. Wait 1–2 seconds, then re-host or re-join. Your room letter + mode are preserved across the stop.

- [ ] **When voice cuts out** — note the wall-clock time and what was happening. Don't tinker with controls; let the session run so the logs capture the recovery (or non-recovery) cleanly.

- [ ] **If one side crashes** — the other side stays in. Note what the surviving side saw (panel state, blank, magenta…) before relaunching the crashed side.

## After the session

### Pull logs from both headsets

Each tester does this on their own machine and shares the file. From the repo root:

```sh
adb=/Applications/Unity/Hub/Editor/6000.4.7f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb

# Pull the most recent log file
LATEST=$($adb shell ls -t /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/ | head -1)
$adb pull /sdcard/Android/data/com.unitymcplab.campfireroom/files/debug-logs/$LATEST ./

# Rename so the recipient can tell whose log it is.
# Replace "johan" with your name.
mv "$LATEST" "johan-$LATEST"
```

Share `johan-campfirevr-log-20260516-140728.jsonl` over Discord / email / etc.

### Optional — also pull native logcat

If a side crashed or had a strange black-screen moment, native Android logs catch things the in-app logger misses. Pull within ~5 minutes of the session (logcat is a ring buffer that overwrites quickly):

```sh
$adb logcat -d -t 5000 > johan-logcat-$(date +%Y%m%d-%H%M%S).txt
```

5,000 lines is usually plenty for a 20-minute session.

### Quick triage on receiving both files

```sh
# Sanity check both files have something
wc -l johan-*.jsonl friend-*.jsonl

# Pretty-print just events + timestamps
jq -c '{ts, event, room, mode}' johan-campfirevr-log-*.jsonl | head -20
jq -c '{ts, event, room, mode}' friend-campfirevr-log-*.jsonl | head -20

# Interleave chronologically (assumes ts is parseable as strings — works
# fine because ts uses ISO format)
{ jq -c '"[johan] " + .ts + " " + .event + " " + (.msg // "")' johan-campfirevr-log-*.jsonl;
  jq -c '"[friend] " + .ts + " " + .event + " " + (.msg // "")' friend-campfirevr-log-*.jsonl; } |
  sort |
  head -60
```

(Skip the `jq` calls and just `cat` the files if you don't have `jq` installed — the JSON lines are short enough to read raw.)

### Find the failure point

For "voice cut out at 14:08:12", grep around that time on both sides:

```sh
grep '14:08:1' johan-campfirevr-log-*.jsonl
grep '14:08:1' friend-campfirevr-log-*.jsonl
```

You'll typically see one of:

- `voice_state` transitioning to `Disconnected` on both sides → Photon Cloud blip; usually recovers on its own.
- `relay_join_failed` on one side → connection drop on the NGO side; voice may still be fine but NGO needs reconnect.
- `unity_error` followed by silence → a crash; logcat will have the stack trace.
- No events at all on one side for several seconds → that side froze or lost focus.

### What to send back for debugging

When filing a report (Discord thread, GitHub issue, whatever):

1. **Both renamed log files** for the failing session.
2. **The wall-clock time** of the failure ("voice cut out around 14:08:12").
3. **One sentence per side** describing what was on the screen and in the headphones at that moment.
4. **Logcat output** (only if a side crashed or black-screened).

That's the full diagnostic kit — enough to reconstruct most multiplayer failures without needing a re-test.

## See also

- [docs/debug-logging.md](debug-logging.md) — what events the logger captures and what fields they have.
- [docs/remote-fika-test.md](remote-fika-test.md) — the original 20-minute structured hangout protocol.
- [docs/install-on-quest.md](install-on-quest.md) — adb setup if this is the first time you're pulling files off a Quest.
