using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR.Management;

// Unity 6 + XR Plugin Management empties PlayerSettings.preloadedAssets during
// the Quest build pipeline (observed: every batchmode build leaves
// ProjectSettings.asset with `preloadedAssets: []` even though it was
// committed with the two XR settings entries). Without those entries the
// next clean build can ship without the XR subsystem initialising at app
// startup — the headset would launch a black 2D-rendered scene.
//
// This guard re-adds the required entries before and after every build and
// saves the result to disk, so the on-disk ProjectSettings.asset matches the
// committed state once the build process exits.
//
// Required preloaded assets:
// - The Android-target XRGeneralSettings sub-asset inside
//   Assets/XR/XRGeneralSettingsPerBuildTarget.asset (drives XR init at runtime)
// - Assets/XR/Settings/OculusSettings.asset (Oculus loader config)
public class PreloadedAssetsGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    // Run after every other build hook so we have the final word on the
    // serialized state. Post-build's int.MaxValue is the safe pick.
    public int callbackOrder => int.MaxValue;

    private const string XRGeneralSettingsPerBuildTargetPath =
        "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
    private const string OculusSettingsPath =
        "Assets/XR/Settings/OculusSettings.asset";

    public void OnPreprocessBuild(BuildReport report) => Restore("pre-build");
    public void OnPostprocessBuild(BuildReport report) => Restore("post-build");

    private static void Restore(string phase)
    {
        var required = ResolveRequiredAssets();
        if (required.Count == 0)
        {
            Debug.LogWarning($"[PreloadedAssetsGuard] {phase}: no XR assets resolved — skipping.");
            return;
        }

        var current = (PlayerSettings.GetPreloadedAssets() ?? new Object[0]).ToList();
        bool changed = false;
        foreach (var asset in required)
        {
            if (!current.Contains(asset))
            {
                current.Add(asset);
                changed = true;
            }
        }

        if (!changed) return;

        PlayerSettings.SetPreloadedAssets(current.ToArray());
        AssetDatabase.SaveAssets();
        Debug.Log($"[PreloadedAssetsGuard] {phase}: restored " +
                  $"{required.Count} XR entries to preloadedAssets " +
                  $"(total now {current.Count}).");
    }

    // Loads the two assets Unity tends to drop. The XRGeneralSettings sub-asset
    // lives inside XRGeneralSettingsPerBuildTarget.asset; we pull the Android
    // one specifically because preloadedAssets stores the build-target-specific
    // settings object, not the container.
    private static List<Object> ResolveRequiredAssets()
    {
        var result = new List<Object>();

        var androidXRSettings = AssetDatabase
            .LoadAllAssetsAtPath(XRGeneralSettingsPerBuildTargetPath)
            .OfType<XRGeneralSettings>()
            .FirstOrDefault();
        if (androidXRSettings != null) result.Add(androidXRSettings);
        else Debug.LogWarning(
            $"[PreloadedAssetsGuard] No XRGeneralSettings sub-asset in " +
            $"{XRGeneralSettingsPerBuildTargetPath}. " +
            $"XR may not init at app startup.");

        var oculus = AssetDatabase.LoadAssetAtPath<Object>(OculusSettingsPath);
        if (oculus != null) result.Add(oculus);
        else Debug.LogWarning(
            $"[PreloadedAssetsGuard] Asset not found at {OculusSettingsPath}. " +
            $"Oculus loader settings will be missing from the build.");

        return result;
    }
}
