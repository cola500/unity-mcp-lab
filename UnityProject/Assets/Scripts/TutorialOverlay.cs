using Unity.Netcode;
using UnityEngine;

public class TutorialOverlay : MonoBehaviour
{
    [SerializeField] private TextMesh text;
    [SerializeField] private float billboardSmoothing = 8f;

    private NetworkBootstrap _net;
    private Camera _cam;

    void Awake()
    {
        if (text == null) text = GetComponentInChildren<TextMesh>();
    }

    void Start()
    {
        _net = FindFirstObjectByType<NetworkBootstrap>();
    }

    void LateUpdate()
    {
        if (text == null) return;

        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
        {
            var toCam = transform.position - _cam.transform.position;
            if (toCam.sqrMagnitude > 0.0001f)
            {
                var target = Quaternion.LookRotation(toCam, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, billboardSmoothing * Time.deltaTime);
            }
        }

        var nm = NetworkManager.Singleton;
        bool connected = nm != null && (nm.IsHost || nm.IsClient);

        string mode  = _net != null ? _net.CurrentMode.ToString() : "";
        string state = _net != null ? _net.CurrentState           : "";

        if (connected)
        {
            text.text = state;
        }
        else
        {
            text.text =
                $"Mode: {mode}\n" +
                "\n" +
                "PRESS X TO HOST\n" +
                "PRESS B TO JOIN\n" +
                "PRESS Y TO SWITCH MODE\n" +
                "PRESS A TO RECENTER\n" +
                "\n" +
                state;
        }
    }
}
