using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot scene hierarchy reorganiser. Groups the ~50 loose environment
// roots in CampfireRoom under a clean parent tree:
//
//   World/
//     Campfire/
//       FirePitKerb/   ← rock_set_* within FirePitRadius of origin
//       (Flame, FireLight, Logs, Embers, FireCrackleAudio, etc)
//     Environment/
//       Forest/
//         Trees/       ← every tree_01 + duplicates
//         Rocks/       ← rock_set_* outside FirePitRadius
//         Mountains/   ← every mountain_terrain_01 + duplicates
//       Grass/         ← GrassBreakup (already a parent of 6 tufts)
//     Seats/           ← Seat_A, Seat_B, StoneSeat_A/B + variants
//     Companions/      ← DogCompanion
//
// Untouched roots (per cleanup spec):
//   VRRig, RemoteRig, NetworkManager, NetworkBootstrap, TutorialPanel,
//   PlayerSlot_A, PlayerSlot_B, EyeHeightMarker_A, Main Camera, World/Ground,
//   World/Atmosphere, World/Directional Light
//
// All reparents use worldPositionStays:true so visible positions don't drift.
// Idempotent: re-runs skip GameObjects already at the correct parent.
// No GameObjects are renamed, no scripts touched, no scene-level visuals changed.
public static class SceneHierarchyOrganize
{
    // Rocks closer than this (XZ-distance) to origin are classified as
    // fire-pit kerbstones; further out = perimeter rocks. The pack uses the
    // same `rock_set_*` prefix for both, so we have to classify by position.
    // Kerbstones sit at ~1.5-2.0m XZ-radius around the central wood pile;
    // perimeter rocks live at 3-9m. 2.5m threshold gives comfortable margin
    // for both. (See ForestSetup.cs + StoneGrounding for the original placement.)
    private const float FirePitRadius = 2.5f;

    private static readonly string[] CampfireDirectChildren = new[]
    {
        "SM_campfire_001",
        "Log_1",
        "Log_2",
        "Flame",
        "FireLight",
        "FireCrackleAudio",
        "Embers",
        "FireStones",
    };

    private static readonly string[] SeatRoots = new[] { "Seat_A", "Seat_B" };

    [MenuItem("Tools/Quest Setup/Organize Scene Hierarchy")]
    public static void Apply()
    {
        var scene = EditorSceneManager.GetActiveScene();

        var world = GameObject.Find("World");
        if (world == null)
        {
            Debug.LogError("[SceneHierarchyOrganize] No 'World' root in the active scene — aborting.");
            return;
        }

        // 1) Build the new parent skeleton (idempotent).
        var campfire     = FindOrCreateChild(world.transform,     "Campfire");
        var firePitKerb  = FindOrCreateChild(campfire,            "FirePitKerb");
        var environment  = FindOrCreateChild(world.transform,     "Environment");
        var forest       = FindOrCreateChild(environment,         "Forest");
        var trees        = FindOrCreateChild(forest,              "Trees");
        var rocks        = FindOrCreateChild(forest,              "Rocks");
        var mountains    = FindOrCreateChild(forest,              "Mountains");
        var grass        = FindOrCreateChild(environment,         "Grass");
        var seats        = FindOrCreateChild(world.transform,     "Seats");
        var companions   = FindOrCreateChild(world.transform,     "Companions");

        // 2) Reparent campfire's immediate visuals/audio/light (some were
        //    already under World/, just descend them one level into Campfire).
        int campfireMoved = 0;
        foreach (var name in CampfireDirectChildren)
        {
            var go = GameObject.Find(name);
            if (go != null && Reparent(go.transform, campfire)) campfireMoved++;
        }

        // 3) Trees + rocks + mountains: iterate scene roots by name prefix.
        var rootGOs = scene.GetRootGameObjects();
        int treesMoved = 0, kerbMoved = 0, perimeterMoved = 0, mountainsMoved = 0;
        foreach (var go in rootGOs)
        {
            if (go == null) continue;
            if (go.name.StartsWith("tree_01"))
            {
                if (Reparent(go.transform, trees)) treesMoved++;
            }
            else if (go.name.StartsWith("rock_set_"))
            {
                var p = go.transform.position;
                float xzDist = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                if (xzDist < FirePitRadius)
                {
                    if (Reparent(go.transform, firePitKerb)) kerbMoved++;
                }
                else
                {
                    if (Reparent(go.transform, rocks)) perimeterMoved++;
                }
            }
            else if (go.name.StartsWith("mountain_terrain_"))
            {
                if (Reparent(go.transform, mountains)) mountainsMoved++;
            }
        }

        // 4) Grass parent (already groups its own 6 tufts).
        int grassMoved = 0;
        var grassBreakup = GameObject.Find("GrassBreakup");
        if (grassBreakup != null && Reparent(grassBreakup.transform, grass)) grassMoved++;

        // 5) Seats: functional roots (Seat_A/B with disabled MeshRenderer) +
        //    visible StoneSeat_* variants. All go under World/Seats.
        int seatsMoved = 0;
        foreach (var name in SeatRoots)
        {
            var go = GameObject.Find(name);
            if (go != null && Reparent(go.transform, seats)) seatsMoved++;
        }
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go != null && go.name.StartsWith("StoneSeat_") && Reparent(go.transform, seats))
                seatsMoved++;
        }

        // 6) Companions.
        int companionsMoved = 0;
        var dog = GameObject.Find("DogCompanion");
        if (dog != null && Reparent(dog.transform, companions)) companionsMoved++;

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "[SceneHierarchyOrganize] Moved: " +
            $"campfire={campfireMoved}, trees={treesMoved}, kerbstones={kerbMoved}, " +
            $"perimeter-rocks={perimeterMoved}, mountains={mountainsMoved}, " +
            $"grass={grassMoved}, seats={seatsMoved}, companions={companionsMoved}. " +
            "Re-run is idempotent.");
    }

    // Returns the existing child with the given name, or creates a new empty
    // GameObject as a child under `parent` with identity local transform.
    private static Transform FindOrCreateChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == childName) return c;
        }
        var go = new GameObject(childName);
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(go, "Create scene-organisation parent");
        return go.transform;
    }

    // Reparents `child` under `newParent`, preserving world position.
    // Returns true when a move actually happened (= newParent != current).
    private static bool Reparent(Transform child, Transform newParent)
    {
        if (child == null || newParent == null) return false;
        if (child.parent == newParent) return false;
        Undo.SetTransformParent(child, newParent, "Organize scene hierarchy");
        child.SetParent(newParent, worldPositionStays: true);
        EditorUtility.SetDirty(child.gameObject);
        return true;
    }
}
