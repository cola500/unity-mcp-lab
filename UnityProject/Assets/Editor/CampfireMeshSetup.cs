using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Wires the Piloto Studio wood-pile mesh as a drop-in replacement for the two
// Log_1 / Log_2 capsule primitives, using a BiRP-friendly material we author
// ourselves (CampfireWood.mat with Standard shader). The Piloto shader pack is
// HDRP-only and unused here — we just borrow the geometry.
//
// Idempotent: re-running re-applies the material assignment and the log-disable
// without duplicating anything.
public static class CampfireMeshSetup
{
    private const string CampfireGameObjectName = "SM_campfire_001";
    private const string WoodMaterialPath = "Assets/Materials/CampfireWood.mat";
    private static readonly string[] LegacyLogNames = { "Log_1", "Log_2" };

    [MenuItem("Tools/Quest Setup/Apply Campfire Mesh")]
    public static void Apply()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath);
        if (mat == null)
        {
            Debug.LogError($"[CampfireMeshSetup] {WoodMaterialPath} not found. Create it first (Standard shader, warm brown).");
            return;
        }

        var campfire = GameObject.Find(CampfireGameObjectName);
        if (campfire == null)
        {
            Debug.LogError($"[CampfireMeshSetup] {CampfireGameObjectName} not in the active scene. Instantiate the Piloto prefab first.");
            return;
        }

        var mr = campfire.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            Debug.LogError($"[CampfireMeshSetup] {CampfireGameObjectName} has no MeshRenderer.");
            return;
        }

        mr.sharedMaterial = mat;
        EditorUtility.SetDirty(mr);
        Debug.Log($"[CampfireMeshSetup] Assigned {mat.name} to {CampfireGameObjectName}.MeshRenderer.sharedMaterial");

        int disabled = 0;
        foreach (var legacyName in LegacyLogNames)
        {
            var go = GameObject.Find(legacyName);
            if (go != null && go.activeSelf)
            {
                go.SetActive(false);
                EditorUtility.SetDirty(go);
                disabled++;
            }
        }
        Debug.Log($"[CampfireMeshSetup] Disabled {disabled} legacy log placeholder(s).");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
