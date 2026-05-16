using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Replaces the rectangular cube placeholders on LeftHandMesh / RightHand-
// Mesh with the XR Interaction Toolkit's UniversalController mesh — the
// official Quest-Touch-style controller silhouette that ships in XRI's
// "Starter Assets" sample. Reads as "a hand holding a controller" in
// headset rather than a rectangle floating in space.
//
// Why a combined mesh, not a prefab instance: the XR Controller prefab
// has ~10 child GameObjects (body + buttons + joystick + trigger), each
// with their own MeshFilter and several scripts (ActionBasedController,
// XR Controller Manager, etc) that we explicitly do NOT want active.
// We use Mesh.CombineMeshes to bake those parts' geometry into one mesh
// at their FBX-local transforms, save it as an asset, and assign that
// single mesh to the existing LeftHandMesh/RightHandMesh MeshFilters.
// Preserves the entire scene hierarchy + XRHeadTracker + XRController-
// InputFeedback's transform.GetChild(0) auto-bind contract.
//
// Anchors, tracking scripts, network components, and XRControllerInput-
// Feedback's _baseScale capture all still work — the hand-anchor's child
// GameObject is untouched as a Transform; only its MeshFilter.sharedMesh,
// MeshRenderer.sharedMaterial, and Transform.localScale change.
//
// Fallback: if XRI Starter Assets isn't importable (package missing,
// import fails), falls back to a tinted sphere primitive so the scene
// is never left with broken cubes.
//
// Idempotent + reversible:
//   - Re-running rebuilds the combined mesh asset in place.
//   - "Apply Hand Visuals (Force Sphere)" explicitly reverts to sphere.
public static class HandVisualsSetup
{
    private const string PackageName = "com.unity.xr.interaction.toolkit";
    private const string StarterAssetsSampleName = "Starter Assets";
    private const string StarterAssetsRootGuess = "Assets/Samples/XR Interaction Toolkit";
    private const string ControllerFbxFilename = "UniversalController.fbx";

    private const string CombinedMeshAssetPath = "Assets/Models/HandsControllerMesh.asset";
    private const string ControllerMaterialPath = "Assets/Materials/HandController.mat";
    private const string SphereMaterialPath = "Assets/Materials/HandSkin.mat";

    private const float ControllerScale = 0.9f;   // FBX is roughly hand-sized already; minor shrink
    private const float SphereScale = 0.07f;      // 7 cm fist

    // Dark warm grey — Quest Touch is darker than skin tone; reads as
    // "controller body" in firelight without competing with the campfire.
    private static readonly Color ControllerTint = new Color(0.22f, 0.20f, 0.18f);
    private static readonly Color SphereTint = new Color(0.72f, 0.52f, 0.42f);

    private static readonly string[] HandMeshNames = { "LeftHandMesh", "RightHandMesh" };

    [MenuItem("Tools/Quest Setup/Apply Hand Visuals")]
    public static void Apply()
    {
        // First-run import. Sample.Import is synchronous enough that the
        // file copy completes before this method returns, but Unity's
        // AssetDatabase import of the copied FBX runs on the next domain
        // refresh — so on the very first run we bail after import and
        // ask the user to re-run.
        if (!EnsureStarterAssetsImported(out string controllerFbxPath))
        {
            Debug.Log("[HandVisualsSetup] Starter Assets sample imported. Re-run 'Apply Hand Visuals' once Unity finishes refreshing.");
            return;
        }

        if (string.IsNullOrEmpty(controllerFbxPath))
        {
            Debug.LogWarning("[HandVisualsSetup] UniversalController.fbx not found after sample import. Falling back to sphere.");
            ApplySphereFallback();
            return;
        }

        var combinedMesh = BuildCombinedMeshFromFbx(controllerFbxPath);
        if (combinedMesh == null)
        {
            Debug.LogWarning("[HandVisualsSetup] Failed to build combined mesh. Falling back to sphere.");
            ApplySphereFallback();
            return;
        }

        var mat = GetOrCreateMaterial(ControllerMaterialPath, "HandController", ControllerTint, smoothness: 0.35f);
        AssignToHandMeshes(combinedMesh, mat, ControllerScale, label: "controller-mesh");
    }

    [MenuItem("Tools/Quest Setup/Apply Hand Visuals (Force Sphere)")]
    public static void ApplySphereFallback()
    {
        var sphereMesh = GetBuiltinMesh("Sphere.fbx", PrimitiveType.Sphere);
        if (sphereMesh == null)
        {
            Debug.LogError("[HandVisualsSetup] Could not obtain built-in Sphere mesh.");
            return;
        }

        var mat = GetOrCreateMaterial(SphereMaterialPath, "HandSkin", SphereTint, smoothness: 0.20f);
        AssignToHandMeshes(sphereMesh, mat, SphereScale, label: "sphere fallback");
    }

    // --- private helpers ------------------------------------------------

    // Returns true when the Starter Assets sample is already imported and
    // the UniversalController FBX path is resolvable. Returns false if
    // the sample was just kicked off — caller should bail and ask user
    // to re-run after Unity finishes its import.
    private static bool EnsureStarterAssetsImported(out string controllerFbxPath)
    {
        controllerFbxPath = null;

        var samples = Sample.FindByPackage(PackageName, string.Empty).ToList();
        if (samples.Count == 0)
        {
            Debug.LogWarning($"[HandVisualsSetup] No samples found for {PackageName}. Is XRI installed?");
            return true; // proceed; will fall back to sphere
        }

        var starter = samples.FirstOrDefault(s => s.displayName == StarterAssetsSampleName);
        if (starter.displayName == null)
        {
            Debug.LogWarning($"[HandVisualsSetup] '{StarterAssetsSampleName}' sample not found.");
            return true;
        }

        if (!starter.isImported)
        {
            bool ok = starter.Import(Sample.ImportOptions.OverridePreviousImports);
            if (!ok)
            {
                Debug.LogError($"[HandVisualsSetup] Failed to import '{StarterAssetsSampleName}'.");
                return true;
            }
            AssetDatabase.Refresh();
            // Returning false signals: import was just done, asset
            // database may not yet have re-imported the FBX. User should
            // re-run the menu after Unity finishes.
            return false;
        }

        controllerFbxPath = FindControllerFbx();
        return true;
    }

    // Search Assets/Samples/XR Interaction Toolkit/<version>/Starter Assets/Models/
    // for UniversalController.fbx. The version sub-folder name is fixed
    // by Unity at sample import time and we don't want to hard-code it.
    private static string FindControllerFbx()
    {
        if (!Directory.Exists(StarterAssetsRootGuess)) return null;

        var matches = Directory.GetFiles(StarterAssetsRootGuess, ControllerFbxFilename, SearchOption.AllDirectories);
        if (matches.Length == 0) return null;

        // Normalise to forward slashes for AssetDatabase.
        return matches[0].Replace('\\', '/');
    }

    // Loads the FBX as a GameObject, walks its MeshFilters, bakes their
    // sharedMesh + localToWorld transforms into one combined Mesh with
    // mergeSubMeshes:true (everything ends up on one submesh, one material).
    // The combined mesh is saved as an asset so it persists across scene
    // reloads and can be re-used across both hand visuals without
    // duplication.
    private static Mesh BuildCombinedMeshFromFbx(string fbxPath)
    {
        var fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbxRoot == null)
        {
            Debug.LogError($"[HandVisualsSetup] Could not load FBX at {fbxPath}.");
            return null;
        }

        // Instantiate so we can read child-MeshFilter transforms cleanly
        // without being affected by the asset's pose. Cleaned up before
        // we return.
        var temp = Object.Instantiate(fbxRoot);
        temp.hideFlags = HideFlags.HideAndDontSave;

        var filters = temp.GetComponentsInChildren<MeshFilter>(includeInactive: false)
            .Where(f => f.sharedMesh != null)
            .ToArray();

        if (filters.Length == 0)
        {
            Object.DestroyImmediate(temp);
            Debug.LogError("[HandVisualsSetup] UniversalController FBX has no MeshFilters.");
            return null;
        }

        var combine = new CombineInstance[filters.Length];
        for (int i = 0; i < filters.Length; i++)
        {
            combine[i].mesh = filters[i].sharedMesh;
            combine[i].transform = filters[i].transform.localToWorldMatrix;
        }

        var combined = new Mesh { name = "HandsControllerMesh" };
        combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // safety for any pack with >65k verts
        combined.CombineMeshes(combine, mergeSubMeshes: true, useMatrices: true);
        combined.RecalculateBounds();

        Object.DestroyImmediate(temp);

        EnsureFolder("Assets/Models");

        // Re-use existing asset slot if present so references stay stable.
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(CombinedMeshAssetPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(combined, existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            return existing;
        }

        AssetDatabase.CreateAsset(combined, CombinedMeshAssetPath);
        AssetDatabase.SaveAssetIfDirty(combined);
        return combined;
    }

    private static void AssignToHandMeshes(Mesh mesh, Material mat, float uniformScale, string label)
    {
        int updated = 0;
        foreach (var name in HandMeshNames)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogWarning($"[HandVisualsSetup] {name} not found in scene.");
                continue;
            }

            var mf = go.GetComponent<MeshFilter>();
            if (mf != null)
            {
                mf.sharedMesh = mesh;
                EditorUtility.SetDirty(mf);
            }

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                EditorUtility.SetDirty(mr);
            }

            go.transform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);
            EditorUtility.SetDirty(go);
            updated++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[HandVisualsSetup] Updated {updated} hand mesh(es) with {label} ({mat.name}, uniform {uniformScale:F2}× scale, shadows off).");
    }

    private static Material GetOrCreateMaterial(string path, string name, Color tint, float smoothness)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[HandVisualsSetup] Standard shader not found.");
                return null;
            }
            EnsureFolder(Path.GetDirectoryName(path));
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_Color", tint);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);
        return mat;
    }

    private static Mesh GetBuiltinMesh(string resourceName, PrimitiveType fallbackType)
    {
        var mesh = Resources.GetBuiltinResource<Mesh>(resourceName);
        if (mesh != null) return mesh;

        var temp = GameObject.CreatePrimitive(fallbackType);
        mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);
        return mesh;
    }

    private static void EnsureFolder(string assetFolder)
    {
        if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder)) return;
        var parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
        var leaf = Path.GetFileName(assetFolder);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
