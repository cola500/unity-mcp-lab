# unity-mcp-lab

A minimal experiment verifying that **Claude Code** can drive the **Unity Editor** through the **Model Context Protocol (MCP)**.

## Vision

A tiny VR social prototype: two people meet in VR, sit by a campfire, and talk. Nothing more.

The current repo is the scaffolding under that goal — first proving the AI ↔ Editor link works, then building the campfire space, and only later adding VR rig and networking. Each slice is small and standalone, so we can stop or change direction at any point.

## Purpose

Prove that an AI assistant can:

1. Read the active scene
2. Create a GameObject
3. Modify its transform
4. Save the scene

…without writing any C# code or clicking in the Unity Editor.

## Stack

| Component | Version |
|---|---|
| Unity Editor | `6000.4.7f1` (Unity 6.4) |
| MCP bridge | [`CoderGamester/mcp-unity`](https://github.com/CoderGamester/mcp-unity) |
| Node.js | ≥ 18 (verified on v26) |
| npm | ≥ 9 (verified on 11.12) |
| Host | macOS Apple Silicon (Rosetta 2 required) |

## Result

Verified end-to-end: `AI_Cube` at position `(0,0,0)` with scale `(2,2,2)` was created and persisted to `UnityProject/Assets/Scenes/Main.unity` via MCP tool calls only.

## Setup on a fresh machine

1. **Install Unity 6** via Unity Hub. Open Hub → Installs → Install Editor → pick any `6000.x` LTS.
2. **Install Rosetta 2** (required even on Apple Silicon for Unity's toolchain):
   ```
   sudo softwareupdate --install-rosetta --agree-to-license
   ```
3. **Clone and resolve the Unity package**:
   ```
   git clone <this-repo> unity-mcp-lab
   cd unity-mcp-lab
   "/Applications/Unity/Hub/Editor/6000.4.7f1/Unity.app/Contents/MacOS/Unity" \
     -batchmode -quit -projectPath "$PWD/UnityProject" -logFile -
   ```
   This populates `UnityProject/Library/PackageCache/com.gamelovers.mcp-unity@<HASH>/`.
4. **Build the Node server**:
   ```
   cd UnityProject/Library/PackageCache/com.gamelovers.mcp-unity@*/Server~
   npm install
   npm run build
   ```
5. **Configure MCP for Claude Code**:
   ```
   cp .mcp.example.json .mcp.json
   ```
   Edit `.mcp.json` and replace `ABSOLUTE_PATH_TO_REPO` and `PACKAGE_HASH` with the real values (find the hash by listing `UnityProject/Library/PackageCache/`).
6. **Open the project in Unity Editor** (Unity Hub → Open → select `UnityProject/`). Leave the Editor running.
7. **In Unity**: `Tools → MCP Unity → Server Window → Start Server`. Keep the window open.
8. **Restart Claude Code** in the repo root. Approve the `.mcp.json` server when prompted.
9. Verify `mcp__mcp-unity__*` tools are loaded by asking Claude to call `get_scene_info`.

## Known issues / friction discovered

- **Rosetta 2 is required** even though the Unity binary is arm64-native. Not documented prominently in mcp-unity's README.
- **`.mcp.json` is brittle**. It embeds:
  - an absolute path to the clone location,
  - a git-commit-prefix hash that changes whenever the `mcp-unity` package is upgraded.
  That is why `.mcp.json` is gitignored and `.mcp.example.json` ships as a template. A more durable setup would move the Node server to a stable in-repo location and reference it relatively, but that is out of scope for this first slice.
- **`Library/PackageCache/` is gitignored** (per Unity convention), so the MCP server path does not exist until Unity has opened the project at least once. Step 3 in setup handles this.
- **`get_gameobject` returns large payloads** (~250 lines per object) including circular references and `Unable to serialize` placeholders. Functional but noisy.
- **Default new scene is unsaved**, so the first save needs `save_scene` with `saveAs: true` and an explicit `scenePath`. Subsequent saves work without it.
- **MCP servers are only loaded at Claude Code session start**. You must restart the session after editing `.mcp.json` or starting the Unity-side server.

## Repository layout

```
unity-mcp-lab/
├── .gitignore
├── .mcp.example.json     # template — copy to .mcp.json and edit
├── README.md
└── UnityProject/         # the Unity 6 project; minimal 3D template
    ├── Assets/Scenes/Main.unity
    ├── Packages/manifest.json
    └── ProjectSettings/
```

## Next slices

Smallest sensible steps toward the vision, in rough order. Each is its own commit/PR.

1. **Flame flicker** — tiny `MonoBehaviour` that perturbs `FireLight` intensity over time (sine + noise). Adds life without networking or VR.
2. **Ambient sound** — a single looping crackle `AudioSource` on the flame. Makes the room feel inhabited.
3. **Night skybox + softer ambient light** — push the mood from "neutral 3D scene" to "evening by a fire".
4. **Look-at gaze on PlayerSlot capsules** — make the placeholder avatars subtly orient toward the fire or each other.
5. **Switch to URP** (if not already) and add a soft bloom on the flame — visual warmth.
6. **XR Interaction Toolkit + a single VR rig at PlayerSlot_A** — first time we put a headset on. Still single-player.
7. **Netcode for GameObjects, host/client over LAN** — two builds, both spawn into a slot, see each other's head. No voice yet.
8. **Voice chat** (Unity Vivox or a peer-to-peer alternative). At this point the original vision is reached.

## Out of scope

No gameplay, no rendering pipeline changes, no polish. The hypothesis was MCP-chain wiring only, and it is confirmed.

## License

MIT — see [LICENSE](LICENSE).
