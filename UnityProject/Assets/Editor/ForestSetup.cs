using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Tidies the forest-atmosphere scene's nature objects (trees, rocks,
// stone seats, mountain backdrops) for Quest. Specifically:
//
//   - Shadow casting OFF on every tree_01, rock_set_*, StoneSeat_*,
//     and mountain_terrain_*. Quest shadow budget is tight with a
//     realtime point-light + directional in the scene.
//   - Shadow receiving OFF on rocks and mountains (they're either
//     small props near the fire or distant backdrops — neither
//     needs detailed shadowing across their surface).
//   - rock_set_* gets scaled to a cozy fire-pit kerb size of 0.4×
//     ONLY if it's still at the prefab's default (1,1,1) scale.
//     Anything the user has already scaled in the Inspector is
//     left alone — protects manual composition tweaks.
//
// Idempotent: safe to re-run any time. Won't override user-set
// transforms, won't duplicate anything.
public static class ForestSetup
{
    private const float DefaultRockScale = 0.4f;

    [MenuItem("Tools/Quest Setup/Apply Forest Setup")]
    public static void Apply()
    {
        int trees = 0, rocks = 0, rocksScaled = 0, seats = 0, mountains = 0;

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (!go.scene.IsValid()) continue; // skip prefab assets
            string n = go.name;

            if (n.StartsWith("tree_01"))
            {
                ConfigureShadowCastingOff(go);
                trees++;
            }
            else if (n.StartsWith("StoneSeat_"))
            {
                // Stone seats are renamed rock prefabs — preserve their user-set
                // scale and rotation, just enforce Quest-safe shadow flags.
                ConfigureShadowCastingOff(go);
                ConfigureShadowReceiveOff(go);
                seats++;
            }
            else if (n.StartsWith("rock_set_"))
            {
                ConfigureShadowCastingOff(go);
                ConfigureShadowReceiveOff(go);
                // Only scale rocks that are still at the default (1,1,1) — i.e.,
                // freshly instantiated and not yet manually placed. This protects
                // user composition tweaks made directly in the Inspector.
                if (IsDefaultScale(go.transform.localScale))
                {
                    go.transform.localScale = Vector3.one * DefaultRockScale;
                    EditorUtility.SetDirty(go);
                    rocksScaled++;
                }
                rocks++;
            }
            else if (n.StartsWith("mountain_terrain_"))
            {
                ConfigureShadowCastingOff(go);
                ConfigureShadowReceiveOff(go);
                mountains++;
            }
        }

        Debug.Log($"[ForestSetup] Configured shadows off — trees:{trees}, rocks:{rocks} ({rocksScaled} scaled to {DefaultRockScale}×), stone seats:{seats}, mountains:{mountains}.");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static void ConfigureShadowCastingOff(GameObject go)
    {
        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null) return;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        EditorUtility.SetDirty(mr);
    }

    private static void ConfigureShadowReceiveOff(GameObject go)
    {
        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null) return;
        mr.receiveShadows = false;
        EditorUtility.SetDirty(mr);
    }

    private static bool IsDefaultScale(Vector3 s)
    {
        const float eps = 1e-4f;
        return Mathf.Abs(s.x - 1f) < eps && Mathf.Abs(s.y - 1f) < eps && Mathf.Abs(s.z - 1f) < eps;
    }
}
