using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerificationCapture
{
    private const string OutputSubDir = "../../docs/verification/abc-join-flow";

    [MenuItem("Tools/Verification/Capture 1 — Idle Relay")]
    public static void Capture1() => Capture("01-idle-relay", net =>
    {
        SetMode(net, "Relay");
        SetEnum(net, "_inputState", "Idle");
        SetField(net, "_hostedAlias", "");
        SetField(net, "_joinCodeInput", "");
        SetField(net, "_state", "Idle");
    });

    [MenuItem("Tools/Verification/Capture 2 — Hosting alias BCA")]
    public static void Capture2() => Capture("02-hosting-bca", net =>
    {
        SetMode(net, "Relay");
        SetEnum(net, "_inputState", "Idle");
        SetField(net, "_hostedAlias", "BCA");
        SetField(net, "_state", "Waiting for friend…");
    });

    [MenuItem("Tools/Verification/Capture 3 — Editor slot 1")]
    public static void Capture3() => Capture("03-editor-slot1", net =>
    {
        SetMode(net, "Relay");
        SetEnum(net, "_inputState", "EditingCode");
        SetField(net, "_hostedAlias", "");
        SetField(net, "_state", "Slot 1 of 3");
        SetField(net, "_slotIndex", 0);
        SetCodeChars(net, 'A', 'A', 'A');
    });

    [MenuItem("Tools/Verification/Capture 4 — Editor slot 3 (B = JOIN)")]
    public static void Capture4() => Capture("04-editor-slot3-join", net =>
    {
        SetMode(net, "Relay");
        SetEnum(net, "_inputState", "EditingCode");
        SetField(net, "_hostedAlias", "");
        SetField(net, "_state", "Slot 3 of 3");
        SetField(net, "_slotIndex", 2);
        SetCodeChars(net, 'B', 'C', 'A');
    });

    private static void Capture(string name, System.Action<NetworkBootstrap> prepare)
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[Verification] Enter Play Mode first, then re-run the capture menu.");
            return;
        }

        var net = Object.FindFirstObjectByType<NetworkBootstrap>();
        if (net == null) { Debug.LogError("[Verification] No NetworkBootstrap in scene"); return; }

        prepare(net);
        EditorApplication.delayCall += () => DoSnap(name);
    }

    private static void DoSnap(string name)
    {
        var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, OutputSubDir));
        Directory.CreateDirectory(outDir);
        var path = Path.Combine(outDir, $"{name}.png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[Verification] Snap queued: {path}");
    }

    private static void SetField(object o, string field, object value)
    {
        var f = o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) f.SetValue(o, value);
        else Debug.LogWarning($"[Verification] Field not found: {field}");
    }

    private static void SetMode(object o, string modeName)
    {
        var f = o.GetType().GetField("mode", BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) f.SetValue(o, System.Enum.Parse(f.FieldType, modeName));
    }

    private static void SetEnum(object o, string field, string valueName)
    {
        var f = o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) f.SetValue(o, System.Enum.Parse(f.FieldType, valueName));
    }

    private static void SetCodeChars(object o, char a, char b, char c)
    {
        var f = o.GetType().GetField("_codeChars", BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.GetValue(o) is char[] arr && arr.Length >= 3)
        { arr[0] = a; arr[1] = b; arr[2] = c; }
    }
}
