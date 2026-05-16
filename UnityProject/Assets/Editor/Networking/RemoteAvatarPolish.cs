using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Tiny visual polish for the PlayerHead.prefab that NGO spawns for each
// remote player. The original prefab uses Unity's built-in default-material
// (white) on a 20 cm sphere head and 7x4x12 cm cube hands — the "white pill
// with two cube nubs" that's been showing up in remote-fika tests.
//
// This swaps to:
//   - HeadVisual:  warm tan-grey "RemotePlayer.mat" (Standard, matte)
//   - LeftHandVisual / RightHandVisual:  same combined controller mesh +
//     dark HandController.mat that the local LeftHandMesh / RightHandMesh
//     now use, scaled uniformly to 0.9x. Makes "your friend's hands" read
//     as the same Quest Touch controllers you see for yourself.
//
// Preserves:
//   - prefab name + GUID
//   - all NetworkObject / ClientNetworkTransform / NetworkHead components
//   - NetworkHead's `visual`/`visualLeft`/`visualRight` SerializeField
//     bindings (we only touch the renderer's mesh + material, not the
//     GameObject identity those refs point at)
//   - prefab structure (no children added/removed)
//
// Idempotent: re-runs assign the same refs, save the same diff.
public static class RemoteAvatarPolish
{
    private const string PrefabPath = "Assets/Prefabs/PlayerHead.prefab";
    private const string RemoteHeadMaterialPath = "Assets/Materials/RemotePlayer.mat";
    private const string HandControllerMaterialPath = "Assets/Materials/HandController.mat";
    private const string HandsControllerMeshPath = "Assets/Models/HandsControllerMesh.asset";

    // Warm soft tan-grey — between the dog coat brown (0.55, 0.38, 0.22)
    // and the sphere-fallback skin peach (0.72, 0.52, 0.42). Reads as
    // "person silhouette" in firelight without competing for attention.
    private static readonly Color RemoteHeadTint = new Color(0.55f, 0.45f, 0.35f);

    [MenuItem("Tools/Quest Setup/Polish Remote Avatar")]
    public static void Apply()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[RemoteAvatarPolish] PlayerHead prefab not found at {PrefabPath}.");
            return;
        }

        var headMat = GetOrCreateRemoteHeadMaterial();
        var handMat = AssetDatabase.LoadAssetAtPath<Material>(HandControllerMaterialPath);
        var handMesh = AssetDatabase.LoadAssetAtPath<Mesh>(HandsControllerMeshPath);

        if (handMat == null)
            Debug.LogWarning($"[RemoteAvatarPolish] {HandControllerMaterialPath} not found — remote hands will keep default material.");
        if (handMesh == null)
            Debug.LogWarning($"[RemoteAvatarPolish] {HandsControllerMeshPath} not found — remote hands will keep cube mesh.");

        // Editing a prefab asset directly is supported via the regular API
        // when we load it as a GameObject. SetDirty + SaveAssets persists.
        var headVisual = FindChildByName(prefab.transform, "HeadVisual");
        var leftHand   = FindChildByName(prefab.transform, "LeftHandVisual");
        var rightHand  = FindChildByName(prefab.transform, "RightHandVisual");

        int updated = 0;

        if (headVisual != null)
        {
            var mr = headVisual.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = headMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                updated++;
            }
        }
        else Debug.LogWarning("[RemoteAvatarPolish] HeadVisual child not found.");

        foreach (var hand in new[] { leftHand, rightHand })
        {
            if (hand == null) continue;
            var mr = hand.GetComponent<MeshRenderer>();
            var mf = hand.GetComponent<MeshFilter>();
            if (mr != null && handMat != null)
            {
                mr.sharedMaterial = handMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            if (mf != null && handMesh != null) mf.sharedMesh = handMesh;
            // Match the local hand mesh proportions so remote hands look
            // like the same controllers, not a stretched rectangle.
            hand.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            updated++;
        }

        PrefabUtility.SavePrefabAsset(prefab);
        AssetDatabase.SaveAssets();

        Debug.Log($"[RemoteAvatarPolish] Updated {updated} visual(s) on PlayerHead.prefab: head→RemotePlayer.mat (warm tan), hands→HandController.mat + combined controller mesh @ 0.9x.");
    }

    private static Material GetOrCreateRemoteHeadMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RemoteHeadMaterialPath);
        if (mat == null)
        {
            var shader = Shader.Find("Standard");
            mat = new Material(shader) { name = "RemotePlayer" };
            AssetDatabase.CreateAsset(mat, RemoteHeadMaterialPath);
        }
        mat.SetColor("_Color", RemoteHeadTint);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.10f);  // matte
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);
        return mat;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            if (c.name == name) return c;
        }
        return null;
    }
}
