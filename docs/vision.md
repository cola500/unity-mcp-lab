# Vision

## What we are building

A cozy, low-poly social VR space. Two people put on Quest 3 headsets, find themselves sitting around a small campfire at night, and talk. That is the whole thing. There is no game, no goal, no progression.

The fire flickers. The other person's head turns when they look at you. You hear their voice from the right direction. When they leave, the seat is empty again.

## Why

Most VR social spaces optimise for crowds, activity, or commerce. We want the opposite: the smallest possible shared space that still feels inhabited. A single room. Two seats. No menus.

If we can make *that* feel real, every later feature — avatars, hands, gestures, shared objects — gets to be additive instead of foundational.

## How we build

**Experimentation first.** The repo's job is to teach us what is hard and what is easy. We push verified end states, not designed plans.

**Tiny vertical slices.** Each commit is one observable change a person could notice in the headset. If a slice is more than a single concept, we split it.

**Claude Code + MCP as the authoring loop.** All scene work, scripts, build settings, and prefab wiring goes through Claude driving the Unity Editor over MCP. Manual Editor clicks are documented in [retro-log.md](retro-log.md) as friction points to remove.

**Stop early, change direction freely.** No slice locks us into a long arc. If something we ship turns out wrong, the next slice rewrites it.

## What this is not

- Not a multiplayer game.
- Not a metaverse.
- Not a platform.
- Not realistic — explicitly cozy low-poly.
- Not a tutorial repo, though it tries to leave readable trails.
