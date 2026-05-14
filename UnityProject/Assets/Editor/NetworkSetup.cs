using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NetworkSetup
{
    private const string PrefabPath = "Assets/Prefabs/PlayerHead.prefab";

    [MenuItem("Tools/Network Setup/Create PlayerHead Prefab")]
    public static void CreatePrefab()
    {
        Directory.CreateDirectory("Assets/Prefabs");
        AssetDatabase.Refresh();

        var root = new GameObject("PlayerHead");
        root.AddComponent<NetworkObject>();
        root.AddComponent<ClientNetworkTransform>();
        root.AddComponent<NetworkHead>();

        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "HeadVisual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * 0.2f;
        Object.DestroyImmediate(visual.GetComponent<SphereCollider>());

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        var nh = prefab.GetComponent<NetworkHead>();
        var visualChild = prefab.transform.Find("HeadVisual");
        var so = new SerializedObject(nh);
        so.FindProperty("visual").objectReferenceValue = visualChild.gameObject;
        so.ApplyModifiedProperties();
        PrefabUtility.SavePrefabAsset(prefab);

        Debug.Log($"[NetworkSetup] Prefab saved: {PrefabPath}");
    }

    [MenuItem("Tools/Network Setup/Configure Scene NetworkManager")]
    public static void ConfigureNetworkManager()
    {
        var nm = Object.FindFirstObjectByType<NetworkManager>();
        if (nm == null) { Debug.LogError("[NetworkSetup] No NetworkManager in scene"); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[NetworkSetup] PlayerHead prefab missing — run Create PlayerHead Prefab first."); return; }

        nm.NetworkConfig.PlayerPrefab = prefab;

        var transport = nm.GetComponent<UnityTransport>();
        if (transport != null)
        {
            nm.NetworkConfig.NetworkTransport = transport;
            transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
        }

        EditorUtility.SetDirty(nm);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[NetworkSetup] NetworkManager configured.");
    }
}
