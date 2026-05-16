# Piloto Studio campfire — evaluation

> Investigation slice. Nothing in the scene was changed. Verdict at the bottom.

## Goal

Decide whether the Piloto Studio Campfires & Torches Pack (locally imported, not committed) gives a meaningful presence upgrade over the current emission-flame-plus-embers setup, without hurting Quest standalone usability or performance.

## What's in the import

`UnityProject/Assets/Piloto Studio/` (gitignored) contains the **Campfires Torches Models and FX** pack with:

- 5 campfire prefab variants × 2 styles each (bare mesh + `_AmbienceFX` with VFX wired):
  `SM_campfire_001` … `SM_campfire_005` and `SM_campfire_00N_AmbienceFX`.
- Plus torches (`SM_torch_*`, `SM_irontorch_*`), candles, planks, and a trashcan with FX. Out of scope for this slice.

### AmbienceFX architecture (same across all 5 variants)

Every `_AmbienceFX` prefab uses the same six-particle stack — they only differ in the wood-arrangement mesh:

| Particle system | Likely role |
|---|---|
| `FireMains` | Tall main flame body |
| `Fire` | Secondary flame layer |
| `EmbersFlickering` | Larger ember sparks |
| `EmbersSmall` | Tiny rising dots |
| `Glow` | Soft halo at base |
| `Rings` | Expanding ring effect |

001/002/003 are 28,472 lines each (byte-identical line counts, so identical FX configs); 004/005 add a second `Glow` and weigh 33,320 lines. **Zero `Light` components**, **zero `AudioSource` components** — the prefab expects you to bring your own light and audio. That part fits us perfectly: we'd reuse `FireLight` + `FireLightFlicker` + `FireCrackleAudio` as-is.

### Hidden surprise: smoke is in there

Despite no GameObject named `Smoke` in the AmbienceFX hierarchy, one of the particle renderers references `Materials/Shared/Smoke_HarsherAlbedo_Alpha_Soft.mat`. So the AmbienceFX prefab **does** have a smoke layer. Per the slice guardrail "no giant smoke systems", that would have needed manual disabling even if shaders were compatible.

## The blocker — HDRP-only shaders, BiRP project

The Piloto Studio **Campfires** pack ships materials but no shaders. Its companion **Piloto Studio Shaders** pack provides the actual `.shader` files. We imported the shaders pack via `Tools/Quest Setup/Import Piloto Studio Shaders` to find out it is **HDRP-only**, not the BiRP-compatible companion the asset inventory speculated about.

Concrete evidence (read off the shader sources after import):

**`Assets/Piloto Studio/Shaders/VFX_Piloto/UberFX.shader`** — used by every fire/glow/flare/smoke material on the AmbienceFX prefabs:

```
SubShader { Tags { "RenderPipeline"="HDRenderPipeline" ... } }
… passes named ForwardOnly / DepthForwardOnly / SceneSelectionPass / Meta / Picking
CustomEditor "Rendering.HighDefinition.LitShaderGraphGUI"
Fallback "Hidden/InternalErrorShader"
```

Compile error in our project:

```
Shader error in 'Piloto Studio/UberFX': Couldn't open include file
'Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl'.
at Assets/Piloto Studio/Shaders/VFX_Piloto/UberFX.shader(114)
```

**`Assets/Piloto Studio/Shaders/SimplyToon_Piloto/Simply Toon.shader`** — used by the **bare** `SM_campfire_001.prefab` mesh material (`GradModels_Toony.mat`): same story, `RenderPipeline=HDRenderPipeline`, same `Fallback "Hidden/InternalErrorShader"`. So even the wood logs (without any FX) would render magenta in BiRP.

What this means for our project:
- The HDRP-only subshader is the only subshader in the file (no BiRP fallback subshader).
- HDRP-only includes (`Packages/com.unity.render-pipelines.core/...`) don't resolve in BiRP.
- Unity falls through to `Hidden/InternalErrorShader` → **the magenta error shader**.
- The slice spec lists "no pink shaders" as a stop condition. Stop condition met.

## What was tried, what was decided

**Tried:**
- Inventoried all 5 variants, picked SM_campfire_001 as lightest cozy candidate (smallest line count, simplest FX stack).
- Imported `Piloto Studio Shaders.unitypackage` from the local Asset Store cache.
- After the magenta verdict, evaluated a hybrid path (Piloto mesh only + our existing FX). Same outcome — the bare-mesh material is also HDRP-only, so the wood logs would render magenta too.

**Decided:**
- **No scene change.** The scene file is untouched.
- **Keep the importer script** (`Editor/PilotoShadersImporter.cs`) as a one-click way to re-import the unitypackage in the future, with a clear comment about the HDRP incompatibility so it's not invoked blindly.

**Followed up: shader import deleted (2026-05-16).** The shader compile errors filling the Editor console were silenced by removing `Assets/Piloto Studio/Shaders/` (and `Shaders.meta`). The other Piloto Studio subfolders (`Campfire And Torches Pack/`, `Materials/`, `Models/`, `Readme/`, `Textures/`, the asset-store PDF) were left in place — they don't compile shaders and don't error on import on their own. The materials under `Materials/Fire/` and `Materials/Shared/` will show as "missing shader" in the Project window if inspected (they reference the deleted UberFX GUID), but they are not in the scene, so they have no rendering or build impact. Re-importing is one menu click away if a future slice wants the pack back.

## Recommendation: stay on the current campfire

The polish-slice campfire (`Polish campfire atmosphere`, commit `076214e`) already gives us:

- A self-illuminating capsule flame via Standard shader emission.
- Subtle drifting embers (24 particles max, additive, Mobile/Particles/Additive shader).
- Light flicker that drifts color as well as intensity.

It runs on Quest at single-digit-millisecond GPU cost, ships in the existing v0.1 APK without any new packages, and renders correctly in BiRP. The Piloto pack would only beat it if (a) we migrate to HDRP/URP (explicitly off the table per slice guardrails and would be a much bigger architectural shift) or (b) we replace the Piloto shaders with our own BiRP-compatible Standard/Particles shaders — losing the "Piloto look" entirely and defeating the point.

**The old fire stays as the default and as the only fire.** No fallback toggle needed — there is nothing to fall back from.

## Future paths if we want to revisit

Two possible next slices, in order of effort:

1. **Replace Piloto fire materials with our own BiRP shaders**, then import only the campfire mesh. Costs the Piloto stylized look but gives us a more interesting wood-log silhouette. ~1 day.
2. **URP migration of the entire project**, then use the Piloto pack as intended. URP is a real lift on a project already using BiRP-specific assets and lighting. ~3–5 days minimum, with high regression risk on the networking/voice/UX work that's already validated. Don't recommend until we have a much stronger reason.

Neither is on the roadmap. Both are documented here so future-us doesn't have to re-derive the analysis.

## Quest risks if we *had* swapped

For the record, even ignoring the magenta blocker:

- 6 particle systems per campfire × overdraw on additive shaders is fine on its own (~24–60 particles total at typical rates, well under Quest's bandwidth limits) — but the bundled smoke layer would have needed manual disabling to stay within "no smoke" guardrail.
- The "Rings" expanding ring effect tends to read as cinematic; would have been the second thing to disable for a cozy/quiet feel.
- Piloto shaders are HDRP — even if forced to compile somehow, expect higher fragment cost than our current Mobile/Particles/Additive setup. Quest tolerates HDRP poorly.

In short: the swap was always going to need significant trimming, on top of the rendering-pipeline blocker.
