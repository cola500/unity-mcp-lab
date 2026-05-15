# Quest tooling — should we add hzdb?

> Investigation only. Nothing installed in this slice. Decision and trade-offs captured here.

## What `@meta-quest/hzdb` is

**Horizon Debug Bridge** — Meta's official, actively maintained CLI + MCP server for Meta Quest development.

| | |
|---|---|
| npm package | `@meta-quest/hzdb` |
| Version (probed) | 1.2.0, published 2026-05-13 |
| First release | 2026-03-07 |
| Maintainers | `joseph-meta` (`@meta.com`) + `zbowling` |
| Repo | https://github.com/meta-quest/agentic-tools |
| Source license | Apache 2.0 (per repo `LICENSE`). The npm metadata reads `"UNLICENSED"` which appears to be a packaging oversight rather than the real intent. |
| Binary distribution | Thin npm wrapper + per-platform native binaries (`darwin-arm64`, `darwin-x64`, `win32-x64` — no Linux yet) downloaded by `postinstall.js`. |

Bullet description from the package itself: *"CLI and MCP server for Meta Quest development, device management, performance tracing, and AI-assisted workflows"*.

## What it can do for us

Command groups (each likely backs multiple MCP tools — Meta says "40+ tools" total):

| Group | Likely capability | Useful for our loop? |
|---|---|---|
| `hzdb device` | List paired Quests, show device info | Replace manual `adb devices` |
| `hzdb app` | Install / launch / uninstall APKs | Replace `adb install -r` + `adb shell am start ...` |
| `hzdb log` | Live logcat | Replace `adb logcat -s Unity:*` |
| `hzdb capture` | **Screenshot / video from headset** | **New capability** — we cannot do this from Editor today |
| `hzdb perf` | Performance traces | For later optimization slices |
| `hzdb files` | Push/pull files between Mac and Quest | E.g., side-loading audio assets |
| `hzdb docs` | Search Meta's Quest docs | Quick reference inside Claude |
| `hzdb shell` / `hzdb adb` | Raw shell / adb passthrough | Escape hatch |
| `hzdb asset` / `hzdb config` / `hzdb mcp` | Asset deployment, runtime config, MCP server management | Less central to our loop |

The single biggest gain for *our* workflow is `hzdb capture`. Today we can verify Editor-side state via screenshot tooling (slice 822ba6d), but the Quest's actual stereo eye output is "what the user says they see". With `hzdb capture` Claude could pull a real headset screenshot directly into the conversation.

## How it would integrate with Claude Code

Per Anthropic's MCP protocol (verified against current Claude Code docs):

| Subcommand | Writes to | Scope |
|---|---|---|
| `npx -y @meta-quest/hzdb mcp install claude-code` | `~/.claude.json` | **User scope** — hzdb appears in every Claude Code project on this machine. |
| `npx -y @meta-quest/hzdb mcp install project` | `.mcp.json` in the current directory | **Project scope** — only this repo gets hzdb tools. Safer for an experimental slice; matches how we set up `mcp-unity`. |

Merge behaviour: a well-behaved installer adds its entry to the existing `mcpServers` object in `.mcp.json` rather than overwriting. We already have `mcp-unity` configured there (gitignored — see `.mcp.example.json` for the template), so merging is the path we want.

After install, **Claude Code must be restarted** for the new tools to load. This is the same friction we hit when adding `mcp-unity` originally. The `/mcp` slash command lists currently-loaded servers and is the way to verify.

## Comparison with alternatives

| Option | Quest-specific? | Captures stereo headset? | Maintenance |
|---|---|---|---|
| **hzdb** (this) | Yes, by Meta | Yes (`capture` group) | Active, official, weekly-ish releases |
| Plain `adb` via Bash | No (Android-generic) | No reliable Quest capture | We use this today |
| Community Android-MCP servers | No | No | Varies, often stale |
| Building our own MCP around `adb screencap` | Possible | Would only capture one eye, no in-headset overlay | Maintenance burden on us |

hzdb is the only option that knows Quest specifically — the others treat it as a generic Android device and miss capabilities like dual-eye capture, OpenXR runtime diagnostics, and Meta's runtime overlays.

## Risks / caveats

1. **License weirdness.** npm registry says `"UNLICENSED"`, repo says Apache 2.0. The package is Meta-official, so this is almost certainly a packaging oversight, but we should note it. For a personal experiment it's fine; for a commercial product we'd want clarification.
2. **Postinstall fetches a native binary.** Trusted publisher (`@meta.com`), but it's a step beyond plain JavaScript — worth knowing.
3. **Restart Claude Code after install.** Standard MCP behaviour. The MCP-Unity server we already depend on needs the same restart, so this isn't new friction.
4. **`.mcp.json` is gitignored in this repo** because it embeds an absolute path to `mcp-unity`'s `Server~/build/index.js`. If `hzdb` adds itself with a path-free `npx` invocation, that line is shareable — but the file as a whole still won't be committed under our current setup. Anyone cloning the repo would need to run the `mcp install project` themselves.
5. **40+ tools could pollute the prompt.** Each MCP tool shows up in Claude's available-tools list. Adding 40 hzdb tools on top of the mcp-unity 30+ we already have means a meaningfully larger prompt envelope per session. Not a blocker, just real.

## Recommendation

**Install with `mcp install project`, not `mcp install claude-code`.**

- Per-project keeps the surface area honest: this is the project that ships to Quest; other projects don't need Quest tools polluting their tool list.
- Per-project merge into `.mcp.json` matches how we set up `mcp-unity` and how we'd document a fresh-clone setup.
- The win is concrete: capture, app install/launch, and live logs let Claude actually verify Quest builds instead of asking "does it look right?".

**Suggested next action** (only after you say go):

```
npx -y @meta-quest/hzdb mcp install project
```

…followed by:

1. Restart Claude Code (`/exit`, re-open the project) so the new server is picked up.
2. Run `/mcp` in the new session to verify hzdb is `connected` and show its tool count.
3. If it looks good, update `.mcp.example.json` so future clones know to install it.

**What still requires manual headset testing**, even with hzdb:

- The subjective feel of presence (no tool can verify "this feels real").
- Comfort (motion, weight, audio level).
- 1:1 networking behaviour with another live user.

hzdb shortens the *technical* verification loop. The fika test loop (`docs/remote-fika-test.md`) is still the right tool for the human-feel loop.

## Workflow doc impact

If we install, the README's "Quest 3 setup" section gets one new bullet under iteration tooling:

> *Optional: `npx -y @meta-quest/hzdb mcp install project` adds Meta's Horizon Debug Bridge as an MCP server, so Claude can install/launch APKs and take headset screenshots without manual `adb` invocations.*

That's all. No architectural change; tooling only.
