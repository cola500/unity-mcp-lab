using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerificationCapture
{
    private const string OutputSubDir = "../../docs/verification/join-flow";

    [MenuItem("Tools/Verification/Capture 1 — Idle Relay (Room A)")]
    public static void Capture1() => Capture("01-idle-room-a", net =>
    {
        SetMode(net, "Relay");
        SetField(net, "_hostedAlias", "");
        SetField(net, "_joinCodeInput", "");
        SetField(net, "_state", "Idle");
        SetLetter(net, 'A');
    });

    [MenuItem("Tools/Verification/Capture 2 — Hosting Room A")]
    public static void Capture2() => Capture("02-hosting-room-a", net =>
    {
        SetMode(net, "Relay");
        SetField(net, "_hostedAlias", "A");
        SetField(net, "_state", "Waiting for friend");
        SetLetter(net, 'A');
    });

    [MenuItem("Tools/Verification/Capture 3 — Idle after cycling to Room D")]
    public static void Capture3() => Capture("03-idle-room-d", net =>
    {
        SetMode(net, "Relay");
        SetField(net, "_hostedAlias", "");
        SetField(net, "_joinCodeInput", "");
        SetField(net, "_state", "Room D");
        SetLetter(net, 'D');
    });

    [MenuItem("Tools/Verification/Capture 4 — Joining Room D")]
    public static void Capture4() => Capture("04-joining-room-d", net =>
    {
        SetMode(net, "Relay");
        SetField(net, "_hostedAlias", "");
        SetField(net, "_joinCodeInput", "D");
        SetField(net, "_busy", true);
        SetField(net, "_state", "Looking for room D");
        SetLetter(net, 'D');
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

    private static void SetLetter(object o, char letter)
    {
        var f = o.GetType().GetField("_codeChars", BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.GetValue(o) is char[] arr && arr.Length >= 1)
            arr[0] = letter;
    }
}
