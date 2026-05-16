using System.IO;
using UnityEditor;
using UnityEngine;

// Imports the Piloto Studio Shaders unitypackage from the local Asset Store cache.
// HDRP-only — does not render in this Built-in Render Pipeline project.
// Materials fall through to Hidden/InternalErrorShader (magenta), and the
// shader compiler logs hundreds of include errors per refresh.
// We imported it once for evaluation, then removed Assets/Piloto Studio/Shaders/
// to silence the Editor console. See docs/piloto-campfire-evaluation.md before
// invoking — re-importing this package will bring those errors back.
public static class PilotoShadersImporter
{
    private const string AssetStoreCachePath =
        "Library/Unity/Asset Store-5.x/Piloto Studio/Shaders/Piloto Studio Shaders.unitypackage";

    [MenuItem("Tools/Quest Setup/Import Piloto Studio Shaders (HDRP-only — see docs)")]
    public static void Import()
    {
        string fullPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            AssetStoreCachePath);

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[PilotoShadersImporter] Package not found at {fullPath}. Download via Unity → My Assets first.");
            return;
        }

        Debug.Log($"[PilotoShadersImporter] Importing {fullPath}");
        AssetDatabase.ImportPackage(fullPath, false);
    }
}
