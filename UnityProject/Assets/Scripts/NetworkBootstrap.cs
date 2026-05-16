using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.XR;

public class NetworkBootstrap : MonoBehaviour
{
    public enum Mode { Lan, Relay }
    public enum Phase { Idle, Hosting, Connecting, Connected }

    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private ushort port = 7777;
    [SerializeField] private Mode mode = Mode.Lan;

    public Mode CurrentMode => mode;
    public string CurrentModeLabel => mode == Mode.Relay ? "Internet" : "Same Wi-Fi";
    public string CurrentState => _state;
    public string LastButton => _lastButton;
    public string LastAction => _lastAction;
    public char CurrentLetter => _codeChars[0];
    public string CurrentRoom => new string(_codeChars);
    public bool LeftHandValid => InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).isValid;
    public bool RightHandValid => InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid;
    public string HostedAlias => _hostedAlias;
    public bool IsBusy => _busy;

    public Phase CurrentPhase
    {
        get
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return Phase.Idle;
            if (nm.IsClient && !nm.IsHost)
                return nm.IsConnectedClient ? Phase.Connected : Phase.Connecting;
            if (nm.IsHost)
                return nm.ConnectedClientsIds.Count >= 2 ? Phase.Connected : Phase.Hosting;
            if (_busy && mode == Mode.Relay)
            {
                if (string.IsNullOrEmpty(_joinCodeInput)) return Phase.Hosting;
                return Phase.Connecting;
            }
            return Phase.Idle;
        }
    }

    private const string LanRoomName = "lan-campfire";
    private const string CodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int CodeLength = 1;
    private const string RelayCodeProperty = "rc";
    private const float StickDeadzone = 0.5f;
    private const float StickRepeatDelay = 0.35f;
    private const float StickRepeatInterval = 0.12f;

    private string _joinCodeInput = "";
    private string _state = "Idle";
    private string _lastButton = "";
    private string _lastAction = "";
    private string _hostedAlias = "";
    private bool _prevLPrimary, _prevLSecondary, _prevRPrimary, _prevRSecondary;
    private bool _loggedLeftInvalid, _loggedRightInvalid;
    private ServicesBootstrap _services;
    private VoiceBootstrap _voiceBootstrap;
    private bool _busy;

    // One slot: the room is a single letter A-Z. Default 'A' so a fresh
    // launch can host / join without the user touching anything.
    private readonly char[] _codeChars = { 'A' };

    private bool _prevStickPos, _prevStickNeg;
    private float _stickPosHeld, _stickNegHeld;
    private float _stickPosNextRepeat, _stickNegNextRepeat;

    private GUIStyle _codeStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _stateStyle;
    private GUIStyle _promptStyle;
    private GUIStyle _modeStyle;

    void Awake()
    {
        _services = GetComponent<ServicesBootstrap>();
        _voiceBootstrap = GetComponent<VoiceBootstrap>();
    }

    void Start()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }
        DebugLogger.Log("network_bootstrap_ready", null,
            ("mode", mode.ToString()),
            ("room", CurrentLetter.ToString()),
            ("scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
    }

    void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    void OnClientConnected(ulong id)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        if (nm.IsHost && id != nm.LocalClientId) _state = "Friend joined";
        else if (nm.IsClient && id == nm.LocalClientId) _state = "Connected";
        DebugLogger.Log("client_connected", null, ("id", (long)id), ("role", nm.IsHost ? "host" : "client"));
    }

    void OnClientDisconnected(ulong id)
    {
        _state = "Friend left";
        DebugLogger.Log("client_disconnected", null, ("id", (long)id));
    }

    void Update()
    {
        if (Application.isEditor)
        {
            if (Input.GetKeyDown(KeyCode.H)) { DebugLogger.Log("editor_key", "H"); StartHost(); }
            if (Input.GetKeyDown(KeyCode.C)) { DebugLogger.Log("editor_key", "C"); StartClient(); }
            if (Input.GetKeyDown(KeyCode.X)) { DebugLogger.Log("editor_key", "X"); Stop(); }
            if (Input.GetKeyDown(KeyCode.M)) { DebugLogger.Log("editor_key", "M"); ToggleMode(); }
            if (Input.GetKeyDown(KeyCode.L)) DebugLogger.Marker("editor_L");
        }

        PollController(XRNode.LeftHand,  ref _prevLPrimary, ref _prevLSecondary, OnLeftPrimary,  OnLeftSecondary);
        PollController(XRNode.RightHand, ref _prevRPrimary, ref _prevRSecondary, OnRightPrimary, OnRightSecondary);

        // Stick cycles the room letter at any time — there's no separate
        // "change room" mode. Default 'A' covers the no-touch case.
        UpdateStickCycle();
    }

    void UpdateStickCycle()
    {
        var rDev = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        Vector2 stick = Vector2.zero;
        if (rDev.isValid) rDev.TryGetFeatureValue(CommonUsages.primary2DAxis, out stick);

        bool pos = false, neg = false;
        if (stick.sqrMagnitude >= StickDeadzone * StickDeadzone)
        {
            float val = Mathf.Abs(stick.x) >= Mathf.Abs(stick.y) ? stick.x : stick.y;
            pos = val > 0f;
            neg = val < 0f;
        }

        TickStick(pos, ref _prevStickPos, ref _stickPosHeld, ref _stickPosNextRepeat, +1);
        TickStick(neg, ref _prevStickNeg, ref _stickNegHeld, ref _stickNegNextRepeat, -1);
    }

    void TickStick(bool active, ref bool prev, ref float heldTime, ref float nextRepeat, int delta)
    {
        if (active && !prev)
        {
            _lastButton = "RightHand stick";
            _lastAction = delta > 0 ? "Stick: next room" : "Stick: prev room";
            CycleLetter(delta);
            heldTime = 0f;
            nextRepeat = 0f;
        }
        if (active)
        {
            heldTime += Time.deltaTime;
            if (heldTime > StickRepeatDelay && heldTime - nextRepeat >= StickRepeatInterval)
            {
                CycleLetter(delta);
                nextRepeat = heldTime;
            }
        }
        else
        {
            heldTime = 0f;
            nextRepeat = 0f;
        }
        prev = active;
    }

    void OnLeftPrimary()
    {
        _lastAction = $"X: host room {CurrentLetter}";
        StartHost();
    }

    void OnLeftSecondary()
    {
        _lastAction = "Y: toggle mode";
        ToggleMode();
    }

    void OnRightPrimary()
    {
        _lastAction = "A: recenter";
        Recenter();
    }

    void OnRightSecondary()
    {
        if (mode == Mode.Lan) _lastAction = "B: join LAN";
        else _lastAction = $"B: join room {CurrentLetter}";
        StartClient();
    }

    void PollController(XRNode node, ref bool prevP, ref bool prevS, System.Action onPrimary, System.Action onSecondary)
    {
        var dev = InputDevices.GetDeviceAtXRNode(node);
        ref bool loggedInvalid = ref (node == XRNode.LeftHand ? ref _loggedLeftInvalid : ref _loggedRightInvalid);

        if (!dev.isValid)
        {
            if (!loggedInvalid)
            {
                Debug.Log($"[Ctrl] {node} device not valid yet (controller off or not paired)");
                loggedInvalid = true;
            }
            return;
        }
        if (loggedInvalid)
        {
            Debug.Log($"[Ctrl] {node} device valid: {dev.name} / {dev.manufacturer} / {dev.characteristics}");
            loggedInvalid = false;
        }

        dev.TryGetFeatureValue(CommonUsages.primaryButton, out bool p);
        dev.TryGetFeatureValue(CommonUsages.secondaryButton, out bool s);
        if (p && !prevP)
        {
            _lastButton = $"{node} primary";
            onPrimary?.Invoke();
        }
        if (s && !prevS)
        {
            _lastButton = $"{node} secondary";
            onSecondary?.Invoke();
        }
        prevP = p; prevS = s;
    }

    void ToggleMode()
    {
        mode = (mode == Mode.Lan) ? Mode.Relay : Mode.Lan;
        _state = $"Mode · {CurrentModeLabel}";
        DebugLogger.Log("mode_changed", null, ("mode", mode.ToString()));
    }

    void Recenter()
    {
        var subs = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subs);
        foreach (var s in subs) s.TryRecenter();
        _state = "Recentered";
        DebugLogger.Log("recenter");
    }

    void CycleLetter(int delta)
    {
        char c = _codeChars[0];
        int i = CodeAlphabet.IndexOf(c);
        if (i < 0) i = 0;
        i = ((i + delta) % CodeAlphabet.Length + CodeAlphabet.Length) % CodeAlphabet.Length;
        _codeChars[0] = CodeAlphabet[i];
        _state = $"Room {_codeChars[0]}";
        DebugLogger.Log("room_changed", null, ("room", _codeChars[0].ToString()));
    }

    async void StartHost()
    {
        if (_busy) return;
        DebugLogger.Log("host_pressed", null, ("mode", mode.ToString()), ("room", CurrentLetter.ToString()));
        if (mode == Mode.Lan)
        {
            _state = "Lighting LAN fire";
            DebugLogger.Log("lan_host_attempt", null, ("address", serverAddress), ("port", (int)port));
            if (!ConfigureLanTransport()) { DebugLogger.Log("lan_host_failed", "transport-config"); return; }
            if (NetworkManager.Singleton.StartHost())
            {
                _state = "Waiting for friend";
                _voiceBootstrap?.JoinRoom(LanRoomName);
                DebugLogger.Log("lan_host_ready");
            }
            else { _state = "Host failed"; DebugLogger.Log("lan_host_failed", "StartHost-returned-false"); }
            return;
        }

        if (_services == null || !_services.IsReady) { _state = "Signing in"; DebugLogger.Log("relay_host_blocked", "services-not-ready"); return; }
        _busy = true;
        _state = "Creating fire";
        DebugLogger.Log("relay_host_attempt", null, ("room", CurrentLetter.ToString()));

        var realCode = await _services.HostRelayAsync();
        if (string.IsNullOrEmpty(realCode))
        {
            _busy = false;
            _state = "Couldn't start fire";
            DebugLogger.Log("relay_alloc_failed");
            return;
        }
        DebugLogger.Log("relay_alloc_succeeded");

        // Host advertises the current room letter (default 'A') as the
        // human-facing alias. Voice room name and discovery property both
        // key off this single letter.
        _hostedAlias = CurrentRoom;
        _state = "Sharing room";
        _voiceBootstrap?.JoinRoom(_hostedAlias);

        bool roomReady = false;
        if (_voiceBootstrap != null) roomReady = await _voiceBootstrap.WaitForRoomJoinedAsync(8f);
        if (roomReady) _voiceBootstrap.SetRoomProperty(RelayCodeProperty, realCode);

        _busy = false;
        _state = roomReady ? "Waiting for friend" : "Voice room failed";
        DebugLogger.Log(roomReady ? "relay_host_ready" : "relay_host_voice_failed");
    }

    async void StartClient()
    {
        if (_busy) return;
        DebugLogger.Log("join_pressed", null, ("mode", mode.ToString()), ("room", CurrentLetter.ToString()));
        if (mode == Mode.Lan)
        {
            _state = "Joining fire";
            DebugLogger.Log("lan_join_attempt", null, ("address", serverAddress), ("port", (int)port));
            if (!ConfigureLanTransport()) { DebugLogger.Log("lan_join_failed", "transport-config"); return; }
            if (NetworkManager.Singleton.StartClient())
            {
                _state = "Joining fire";
                _voiceBootstrap?.JoinRoom(LanRoomName);
                DebugLogger.Log("lan_join_started");
            }
            else { _state = "Join failed"; DebugLogger.Log("lan_join_failed", "StartClient-returned-false"); }
            return;
        }

        if (_services == null || !_services.IsReady) { _state = "Signing in"; DebugLogger.Log("relay_join_blocked", "services-not-ready"); return; }

        // Always join the currently selected room letter (default 'A').
        _joinCodeInput = CurrentRoom;
        var alias = _joinCodeInput;
        _busy = true;
        _state = $"Looking for room {alias}";
        DebugLogger.Log("relay_join_attempt", null, ("room", alias));

        _voiceBootstrap?.JoinRoom(alias);

        bool joined = false;
        if (_voiceBootstrap != null) joined = await _voiceBootstrap.WaitForRoomJoinedAsync(8f);
        if (!joined)
        {
            _busy = false;
            _state = "No fire found";
            DebugLogger.Log("relay_join_voice_timeout", null, ("room", alias));
            return;
        }

        var realCode = await _voiceBootstrap.WaitForRoomPropertyAsync(RelayCodeProperty, 5f);
        if (string.IsNullOrEmpty(realCode))
        {
            _busy = false;
            _state = "Host's code missing";
            DebugLogger.Log("relay_join_property_missing", null, ("room", alias));
            return;
        }

        _state = "Joining fire";
        DebugLogger.Log("relay_join_calling");
        bool ok = await _services.JoinRelayAsync(realCode);
        _busy = false;
        if (!ok) { _state = "Couldn't reach fire"; DebugLogger.Log("relay_join_failed"); }
        else DebugLogger.Log("relay_join_succeeded");
    }

    async void Stop()
    {
        DebugLogger.Log("stop_pressed");
        _voiceBootstrap?.LeaveRoom();
        if (_services != null && _services.InRelaySession)
            await _services.LeaveRelayAsync();
        var nm = NetworkManager.Singleton;
        if (nm != null && (nm.IsHost || nm.IsClient)) nm.Shutdown();
        _state = "Disconnected";
        _joinCodeInput = "";
        _hostedAlias = "";
        DebugLogger.Log("stopped");
    }

    bool ConfigureLanTransport()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) { _state = "No NetworkManager"; return false; }
        var t = nm.GetComponent<UnityTransport>();
        if (t == null) { _state = "No UnityTransport"; return false; }
        t.SetConnectionData(serverAddress, port, "0.0.0.0");
        return true;
    }

    void EnsureStyles()
    {
        if (_codeStyle != null) return;
        _codeStyle = new GUIStyle(GUI.skin.label) { fontSize = 96, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
        _stateStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
        _promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        _promptStyle.normal.textColor = new Color(1f, 0.85f, 0.62f, 0.85f);
        _modeStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        _modeStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 0.6f);
    }

    void OnGUI()
    {
        if (!Application.isEditor) return;

        EnsureStyles();

        var nm = NetworkManager.Singleton;
        bool connected = nm != null && (nm.IsHost || nm.IsClient);

        float w = Screen.width;
        GUI.Label(new Rect(0, 20, w, 24), $"Mode: {mode}", _modeStyle);

        if (Application.isEditor)
        {
            GUI.Label(new Rect(20, 50, 1100, 24),
                $"Local IPs: {string.Join(", ", GetLocalIPv4())}    (editor keys: H host, C join, M mode, X stop)");
        }

        float topY = Screen.height * 0.18f;
        if (!string.IsNullOrEmpty(_hostedAlias))
        {
            GUI.Label(new Rect(0, topY, w, 40), "ROOM", _labelStyle);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.5f);
            var prev = GUI.color;
            GUI.color = Color.Lerp(new Color(1f, 0.72f, 0.42f), new Color(1f, 0.88f, 0.62f), pulse);
            GUI.Label(new Rect(0, topY + 50, w, 140), _hostedAlias, _codeStyle);
            GUI.color = prev;
        }
        else if (!connected)
        {
            GUI.Label(new Rect(0, topY, w, 40), "ROOM", _labelStyle);
            GUI.Label(new Rect(0, topY + 50, w, 140), CurrentRoom, _codeStyle);
        }

        GUI.Label(new Rect(0, Screen.height * 0.70f, w, 40), _state, _stateStyle);

        if (mode == Mode.Relay && Application.isEditor && !connected)
        {
            GUI.Label(new Rect(20, 80, 220, 28), "Editor room override:");
            var typed = (GUI.TextField(new Rect(240, 80, 60, 28), CurrentRoom, CodeLength) ?? "").ToUpper();
            if (!string.IsNullOrEmpty(typed) && CodeAlphabet.IndexOf(typed[0]) >= 0)
                _codeChars[0] = typed[0];
        }
    }

    static IEnumerable<string> GetLocalIPv4()
    {
        IPHostEntry entry;
        try { entry = Dns.GetHostEntry(Dns.GetHostName()); } catch { yield break; }
        foreach (var ip in entry.AddressList)
            if (ip.AddressFamily == AddressFamily.InterNetwork) yield return ip.ToString();
    }
}
