using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class MicrophoneTest : MonoBehaviour
{
    [SerializeField] private int sampleWindow = 256;
    [SerializeField] private float meterGain = 5f;

    private AudioClip _clip;
    private string _device;
    private float _level;
    private string _status = "Mic: not started";
    private bool _permissionAsked;

    void Start()
    {
        TryStartMicrophone();
    }

    void Update()
    {
        if (_clip == null) TryStartMicrophone();
        if (_clip != null) UpdateLevel();
    }

    void TryStartMicrophone()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            if (!_permissionAsked)
            {
                Permission.RequestUserPermission(Permission.Microphone);
                _permissionAsked = true;
                _status = "Mic: requesting permission…";
            }
            return;
        }
#endif

        var devices = Microphone.devices;
        if (devices.Length == 0)
        {
            _status = "Mic: no devices";
            return;
        }

        _device = devices[0];
        _clip = Microphone.Start(_device, true, 1, AudioSettings.outputSampleRate);
        _status = $"Mic: capturing on {_device}";
    }

    void UpdateLevel()
    {
        int pos = Microphone.GetPosition(_device);
        if (pos < sampleWindow) return;

        var samples = new float[sampleWindow];
        _clip.GetData(samples, pos - sampleWindow);

        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];
        _level = Mathf.Sqrt(sum / samples.Length);
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        GUI.Label(new Rect(20, 250, 800, 28), _status, style);
        GUI.Label(new Rect(20, 278, 800, 28), $"Devices: {string.Join(", ", Microphone.devices)}", style);
        GUI.Label(new Rect(20, 306, 800, 28), $"Level (RMS): {_level:F3}", style);

        var bar = new Rect(20, 338, 400, 24);
        GUI.Box(bar, GUIContent.none);
        float t = Mathf.Clamp01(_level * meterGain);
        var fill = new Rect(bar.x + 2, bar.y + 2, (bar.width - 4) * t, bar.height - 4);
        var prev = GUI.color;
        GUI.color = Color.Lerp(new Color(0.4f, 0.4f, 0.4f), new Color(1f, 0.72f, 0.3f), t);
        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = prev;
    }
}
