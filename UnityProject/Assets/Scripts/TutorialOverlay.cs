using System.Text;
using UnityEngine;

public class TutorialOverlay : MonoBehaviour
{
    [SerializeField] private TextMesh text;
    [SerializeField] private float billboardSmoothing = 8f;
    [SerializeField] private float notificationSeconds = 3f;

    private NetworkBootstrap _net;
    private Camera _cam;
    private string _lastSeenState = "";
    private float _notificationUntil;

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

        if (_net == null) { text.text = ""; return; }

        string state = _net.CurrentState ?? "";
        if (state != _lastSeenState)
        {
            _lastSeenState = state;
            if (!string.IsNullOrEmpty(state)) _notificationUntil = Time.time + notificationSeconds;
        }
        bool showNotification = Time.time < _notificationUntil;

        switch (_net.CurrentPhase)
        {
            case NetworkBootstrap.Phase.Joining:
            {
                int last = _net.CodeLengthSlots - 1;
                bool onLast = _net.CodeSlot >= last;
                text.text =
                    "🔥  JOIN FIRE\n" +
                    "\n" +
                    _net.CodeDisplay + "\n" +
                    "\n" +
                    "A / X   change\n" +
                    (onLast ? "B       join\n" : "B       next\n") +
                    "Y       back";
                break;
            }

            case NetworkBootstrap.Phase.Hosting:
            {
                if (!string.IsNullOrEmpty(_net.HostedAlias))
                {
                    text.text =
                        "🔥  YOUR FIRE\n" +
                        "\n" +
                        "code\n" +
                        SpaceLetters(_net.HostedAlias) + "\n" +
                        "\n" +
                        "waiting for friend";
                }
                else
                {
                    text.text =
                        "🔥  YOUR FIRE\n" +
                        "\n" +
                        "waiting for friend";
                }
                break;
            }

            case NetworkBootstrap.Phase.Connected:
                text.text = showNotification ? "🔥  " + state : "";
                break;

            case NetworkBootstrap.Phase.Idle:
            default:
                text.text =
                    "🔥  CAMPFIRE\n" +
                    "\n" +
                    "X    host\n" +
                    "B    join" +
                    (showNotification ? "\n\n" + state : "");
                break;
        }
    }

    static string SpaceLetters(string code)
    {
        if (string.IsNullOrEmpty(code)) return "";
        var sb = new StringBuilder(code.Length * 2);
        for (int i = 0; i < code.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(code[i]);
        }
        return sb.ToString();
    }
}
