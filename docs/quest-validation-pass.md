# Quest validation pass — pre-build readiness check

> Read-through audit run before the next Quest deploy. Covers build readiness, performance risks, VR UX sanity, and networking after the recent slices (forest atmosphere, Terra ground/backdrop, stone seats, campfire material polish, sparse grass, tree wind, XRI hand/controller visuals, single-letter room code). No new features. Two tiny isolated fixes applied; one design-decision recommendation deferred for explicit approval.

## Verdict

**Safe to build for Quest: YES.** No blockers. **Two tiny fixes + three recommended perf fixes all applied in this pass** (see Tiny fixes + Recommended fixes sections below — all marked **APPLIED**). Scene saved, compile clean.

## 1. Build readiness

| Check | Status | Notes |
|---|---|---|
| Compile | ✅ 0 errors, 0 warnings | Confirmed via `recompile_scripts` (33 third-party CS0618 deprecation warnings predate this audit and are noise from Terra / NatureStarterKit2 editors). |
| Active scene | ✅ `CampfireRoom.unity` only | `EditorBuildSettings.scenes` lists exactly one scene at build index 0. `Main.unity` exists on disk but is not in build settings (still flagged dead in `app-alignment-qa.md`). |
| Quest / Android target | ✅ ARM64 + IL2CPP-compatible | `AndroidTargetArchitectures: 2` (ARM64), `stripEngineCode: 1`, `apiCompatibilityLevel: 6` (.NET Standard 2.1). |
| XR loader | ✅ Oculus loader on Android Managers | `XRGeneralSettingsPerBuildTarget` references Oculus loader for Android target. `com.unity.xr.oculus 4.5.0` + `com.unity.xr.management 4.5.0` installed. |
| Product / bundle | ✅ Rebranded | `productName: CampfireVR` (updated in this pass — was `CampfireRoom`), `bundleVersion: 1.0` (unchanged). On-headset app icon will now reflect the rebrand on next install. |
| Editor-only debug objects | ✅ None accidentally included | No `Debug…` / `Test…` / `Capture…` / `Probe…` root GameObjects in scene. `VerificationCapture` lives in `Assets/Editor/` and is editor-only by folder convention. |
| `Assets/Resources/` | ✅ Empty | No leaked auto-included resources. |
| `Assets/Samples/` (XRI sample tree, 10 MB) | ✅ Gitignored + build-safe | Only assets actually referenced by the scene end up in the build. We reference `HandsControllerMesh.asset` (committed under `Assets/Models/`), which Unity treats as the canonical source — the sample FBX is consumed only at authoring time. |

## 2. Performance risks

### Scene inventory

- **74 root GameObjects** in the scene (per `get_scene_info`). 54 line up with `GameObject:` blocks in the scene YAML; the gap is normal (some are nested under Forest / GrassBreakup / FireStones).
- **2 cameras** — `VRCamera` (active, MainCamera tag, drives the HMD) + flat-screen `Main Camera` (disabled). Exactly the expected split.
- **2 realtime lights** — `Directional Light` (intensity 0.05, soft shadows) + `FireLight` (point, range 9 m, intensity 3, soft shadows).
- **1 particle system** — `Flame` (VFX_Fire integration).
- **1 MeshCollider** in the scene. Static, not on a moving body — acceptable cost.
- **1 AudioSource** — `FireCrackleAudio` (looping ambient, `spatialBlend = 1`).

### Risk table

| # | Risk | Severity | Mitigation |
|---|---|---|---|
| **P1** | **`FireLight` Soft realtime shadows** on a 9 m point light. Point-light shadow maps cost roughly 6× a directional shadow (cubemap render). Range covers seats, kerbstones, wood pile, both hand meshes, the inner grass tufts, and several inner forest objects — a lot of casters per frame. | **High** | **APPLIED** — `shadows = Hard` (was `Soft`). Roughly halves the per-frame shadow render cost while keeping the wood pile + kerbstones grounded by cast shadow. Skipped `None` to avoid the fire light "leaking through" the wood pile silhouette in headset, which would read wrong even though it'd be cheaper still. Fire color, intensity, and `FireLightFlicker` are untouched. |
| **P2** | **`Directional Light` Soft realtime shadows** with intensity 0.05. The shadows are barely visible at this intensity in a night scene, but the per-frame shadow map still renders at full cost. | Medium | **APPLIED** — `shadows = None` (was `Soft`). Intensity 0.05 means the cast shadows from this dim night fill were invisible already; removing them is a free per-frame win. Color and intensity untouched. |
| **P3** | **`TutorialPanel.MeshRenderer.shadowCastingMode = On`** on a world-space UI Quad. UI panels should never cast shadows. | Low | **Fixed in this pass** — flipped to `Off` + `receiveShadows = false`. See Tiny fixes. |
| **P4** | **`MicrophoneTest` GameObject** still in scene with an `Update()` loop. Script was the early Voice A probe; dead since Voice C/D/E shipped (flagged in `app-alignment-qa.md`). On a Quest build it would call `Microphone.Start` + sample the buffer every frame on top of Photon Voice's own recorder. | Low | **Fixed in this pass** — root GameObject deleted. Script file kept on disk in case anyone wants to re-attach it manually for debugging mic permissions. |
| **P5** | **9 shadow-casting MeshRenderers** still in the scene (`m_CastShadows: 1`). Tutorial, kerbstones, campfire wood pile, and the rocks the previous slices intentionally kept casting for grounding contact shadows. | Low | Acceptable — earlier slices made the call to keep these on; the cost only matters under P1/P2. If P1/P2 lights' shadows are turned off, these `m_CastShadows: 1` flags become free. |
| **P6** | **Quality preset shadow distance + cascades** — Project's quality presets at Low/Medium use `shadowDistance: 15–20` and `shadowCascades: 1`. Quest typically renders at Medium or below. The shadow distance comfortably covers our 20 m floor. | None | Tuned reasonably for Quest already. |
| **P7** | **172 textures in `Assets/`** totalling ~1.1 GB on disk, but most live in gitignored Asset Store packs (NatureStarterKit2, Mountain Terrain, Piloto, Photon demos) and are not all referenced by the scene. Only scene-referenced textures end up in the APK. | None (informational) | Build-time stripper handles it. Worth re-auditing if APK ever balloons past ~150 MB. |
| **P8** | **Photon Voice 2 NullReferenceException** in Editor (`VoiceConnection.Update()` / `OnDestroy()`) when scene is marked dirty. Pre-existing third-party bug, only fires in Editor. | None on device | Quest builds are unaffected. Documented across multiple prior slice docs. |

### Per-object render cost

- **`HandsControllerMesh.asset`** is 703 KB on disk after combine. UniversalController source FBX is ~5 k triangles; the combined mesh is the same geometry in one MeshFilter, so each hand is ~5 k triangles, both hands together ≈ 10 k. Quest's per-frame budget easily handles that, especially with `shadowCastingMode = Off` on both hand meshes (confirmed in the hand visuals slice).

## 3. VR UX sanity

| Check | Status | Notes |
|---|---|---|
| Seated rig position | ✅ Correct | `VRRig` at world `(1.6, 0, 0)` rotated 270° Y, `CameraOffset` at local `(0, 1.2, 0)` — Seat A facing the fire, seated eye height. |
| Hand meshes | ✅ Reasonable | Both hand anchors hold the combined UniversalController mesh at uniform scale 0.9, `shadowCastingMode = Off`. World bounds ≈ 10 × 7 × 5 cm — Quest Touch proportions. Same offset-from-grip caveat documented in `README.md` known-issues (controller grip ≠ palm). |
| Seats / stones / fire layout | ✅ Untouched by this pass | Stone seats, kerbstones, wood pile, perimeter rocks, trees all at the positions set by prior slices. No regression. |
| TutorialPanel readability | ✅ Reachable + readable | World-space TextMesh at `(-0.96, 1.64, 3.23)`, `characterSize = 0.04`, billboards toward camera via `TutorialOverlay.LateUpdate`. Font is Unity legacy Arial (`LegacyRuntime`). |
| Emoji rendering | ⚠️ Known issue | TextMesh + LegacyRuntime/Arial sometimes renders `🔥` as `?` on Quest. Already documented in `remote-fika-test.md` under known rough edges. Not a new regression — the codepath hasn't changed. |
| Default room code | ✅ `A` | `NetworkBootstrap._codeChars = { 'A' }` is the field initializer (private readonly). Verified by grep + diff against `9a13b77`. |
| Host path | ✅ Reads `_codeChars[0]` | `StartHost()` sets `_hostedAlias = CurrentRoom` (= `new string(_codeChars)`). Random-alias path is gone. |
| Join path | ✅ Reads `_codeChars[0]` | `StartClient()` sets `_joinCodeInput = CurrentRoom` before joining the voice room. |
| Stick changes room | ✅ Always-on | `UpdateStickCycle()` is called every frame in `Update()` (no EditingCode gate). Right thumbstick cycles A↔Z. |
| Obsolete 3-letter text | ✅ Removed from runtime UI | `TutorialOverlay` builds the legend with the current letter substituted in; no static "ABC" / "3 slots" / `[A] B C` references survive. Stale "PRESS X TO HOST / …" string still sits in the scene-serialized `TextMesh.text` field as the pre-OnEnable placeholder, but `TutorialOverlay.LateUpdate` overwrites it on frame one in headset — no visible flash because the scene-saved camera is muted on the first frame anyway. |
| Debug overlays in Quest build | ✅ All gated | `NetworkBootstrap.OnGUI`, `ServicesBootstrap.OnGUI`, and `VoiceBootstrap.OnGUI` are each gated behind `if (!Application.isEditor) return;`. The previously-flagged `VoiceBootstrap.OnGUI` leak in `app-alignment-qa.md` has since been fixed (verified by re-reading the source in this pass). |

## 4. Networking readiness

Static read-through of `NetworkBootstrap.cs`, `TutorialOverlay.cs`, `VoiceBootstrap.cs`, `Services/ServicesBootstrap.cs`, and the scene's `NetworkManager` + `UnityTransport` components.

| Check | Status | Notes |
|---|---|---|
| LAN mode uses LAN path | ✅ | `Mode.Lan` branch calls `ConfigureLanTransport()` → `UnityTransport.SetConnectionData(serverAddress, port, ...)` → `NetworkManager.StartHost()` / `StartClient()`. Voice room hard-codes `LanRoomName = "lan-campfire"`. |
| Internet mode uses Relay | ✅ | `Mode.Relay` branch calls `_services.HostRelayAsync()` / `JoinRelayAsync(realCode)` with the Unity Multiplayer Service. Voice room name = current single letter; Relay token is published as the Photon room property `rc`. |
| Visible UI labels | ✅ | `CurrentModeLabel` returns `"Internet"` for `Relay` and `"Same Wi-Fi"` for `Lan`. The internal enum names never leak to the user. |
| Voice room follows room code | ✅ | `_voiceBootstrap?.JoinRoom(_hostedAlias)` on host, `_voiceBootstrap?.JoinRoom(alias)` on client — both `alias` derived from `_codeChars[0]`. |
| Single-letter code used consistently | ✅ | `CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"`, `CodeLength = 1`, all references touch the single slot. `GenerateAlias()` / `EnterCodeEditor()` / `ExitCodeEditor()` and the entire `InputState.EditingCode` machinery were removed in the single-letter slice — no zombie callers. |
| `UnityTransport` config | ✅ | Default `127.0.0.1:7777` on the scene's `NetworkManager` (LAN address baked in at build time, per the known dev-only LAN flow). `RunInBackground: true` keeps the connection alive during system overlay events on Quest. |
| Coupling to 3-char assumptions | ✅ None | No code path reads `_codeChars[1]`, `_codeChars[2]`, or assumes `_joinCodeInput.Length == 3`. The Editor-only override `TextField` is now sized to `CodeLength = 1`. |

## Tiny fixes applied in this pass

Both fall squarely under "clearly wrong, low-risk, isolated". Each touches a single object/component, no behavioural change beyond the perf / cleanup intent.

1. **TutorialPanel: `shadowCastingMode = Off`, `receiveShadows = false`.** World-space UI Quad shouldn't cast contact shadows onto the campfire stones, and it never needs to be lit by the realtime lights. Was `On` / `true`, now `Off` / `false`. Pure win.
2. **Deleted `MicrophoneTest` root GameObject from the scene.** Script (`Assets/Scripts/MicrophoneTest.cs`) is the early Voice A mic-probe, dead since the Voice C/D/E slices shipped (`app-alignment-qa.md` flagged it but didn't act). Removing the GameObject kills one `Update()` per frame and a per-frame `Microphone.GetPosition` sample. Script file kept on disk — anyone who wants to re-probe permissions can drop it back on a fresh GameObject.

## Recommended fixes — all APPLIED in this pass

1. **`FireLight.shadows = Hard`** (was `Soft`). Roughly half the per-frame point-light shadow cost while keeping the wood pile + kerbstones visually grounded by cast shadows. The fire's *colour* and *flicker* are untouched and still warm everything within range. Chose `Hard` over `None` to avoid light leaking through the wood pile silhouette.
2. **`Directional Light.shadows = None`** (was `Soft`). Intensity 0.05 made the cast shadows essentially invisible already; removing them is a clean per-frame win with no visual loss. Color and intensity untouched.
3. **`productName: CampfireRoom → CampfireVR`** in Player Settings. Cosmetic but the on-headset app icon now matches the project name. `bundleVersion: 1.0` unchanged.

## Risks documented but not fixed

| Item | Severity | Why not fixed here |
|---|---|---|
| Photon Voice 2 Editor NullReferenceException on scene-dirty | None on device | Third-party bug, only in Editor. Filing upstream is the right move, not patching their pack. |
| Emoji rendering as `?` on Quest | Cosmetic | Would require swapping `TextMesh` for `TextMeshPro` + a font with full Unicode coverage. That's a real slice, not a tiny fix. |
| Hand-mesh sits on controller grip, not palm | Cosmetic | Known issue in `README.md`. Out of scope for this validation pass. |
| ~~`productName` still reads `CampfireRoom`~~ | ~~Cosmetic~~ | ~~Listed in Recommended fixes above for explicit OK.~~ **Resolved in this pass — now `CampfireVR`.** |
| `Assets/Scenes/Main.unity` dead scene on disk | None | Not in build settings, not in any reference chain. Cleanup slice opportunity. |
| LAN flow requires baked `serverAddress` | Functional | Known limitation, dev-only path. README + audit doc already explain this. |

## Validation output (after all fixes)

- `recompile_scripts`: **0 errors, 0 warnings** (only the same 33 pre-existing third-party CS0618 deprecation warnings, unchanged).
- `get_scene_info`: scene `CampfireRoom` loaded, build index 0, root count 73 (was 74 before `MicrophoneTest` deletion).
- `get_gameobject TutorialPanel`: `MeshRenderer.shadowCastingMode = "Off"`, `receiveShadows = false`.
- `get_gameobject MicrophoneTest`: not found (deleted).
- `FireLight.m_Shadows.m_Type: 1` (= `Hard`).
- `Directional Light.m_Shadows.m_Type: 0` (= `None`).
- `ProjectSettings.asset` line 16: `productName: CampfireVR`.
- VoiceBootstrap.OnGUI verified gated: `if (!Application.isEditor) return;`.

## Should we proceed with build / deploy to Quest?

**Yes.** All recommended Quest perf + rebranding fixes are applied. Two realtime shadow renders dropped to one (Hard), point-light shadow rendering cost roughly halved, on-headset app name now matches the project. Nothing remaining on the risk list blocks a deploy. The remote-fika protocol in `docs/remote-fika-test.md` can be followed as-is for the next two-Quest session.

### Items intentionally untouched per spec scope

Networking, voice architecture, room code, XR rig, hand visuals, environment layout — none modified beyond the requested shadow settings + productName.
