using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.XR;

public class NetworkBootstrap : MonoBehaviour
{
    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private ushort port = 7777;

    private string _status = "Idle";
    private bool _prevA, _prevB;

    void Update()
    {
        if (Application.isEditor)
        {
            if (Input.GetKeyDown(KeyCode.H)) StartHost();
            if (Input.GetKeyDown(KeyCode.C)) StartClient();
            if (Input.GetKeyDown(KeyCode.X)) Stop();
        }

        var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (right.isValid)
        {
            right.TryGetFeatureValue(CommonUsages.primaryButton, out bool a);
            right.TryGetFeatureValue(CommonUsages.secondaryButton, out bool b);
            if (a && !_prevA) StartHost();
            if (b && !_prevB) StartClient();
            _prevA = a; _prevB = b;
        }
    }

    void StartHost()
    {
        if (!ConfigureTransport()) return;
        if (NetworkManager.Singleton.StartHost()) _status = $"HOST on :{port}";
        else _status = "Host start failed";
    }

    void StartClient()
    {
        if (!ConfigureTransport()) return;
        if (NetworkManager.Singleton.StartClient()) _status = $"CLIENT → {serverAddress}:{port}";
        else _status = "Client start failed";
    }

    void Stop()
    {
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        _status = "Stopped";
    }

    bool ConfigureTransport()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) { _status = "No NetworkManager in scene"; return false; }
        var t = nm.GetComponent<UnityTransport>();
        if (t == null) { _status = "No UnityTransport on NetworkManager"; return false; }
        t.SetConnectionData(serverAddress, port, "0.0.0.0");
        return true;
    }

    void OnGUI()
    {
        GUI.Label(new Rect(20, 20, 800, 30), $"Net: {_status}    (H=Host, C=Client, X=Stop  |  A=Host, B=Client on right controller)");
        GUI.Label(new Rect(20, 50, 800, 30), $"Local IPs: {string.Join(", ", GetLocalIPv4())}");
        GUI.Label(new Rect(20, 80, 800, 30), $"Target server: {serverAddress}:{port}");
    }

    static IEnumerable<string> GetLocalIPv4()
    {
        IPHostEntry entry;
        try { entry = Dns.GetHostEntry(Dns.GetHostName()); }
        catch { yield break; }
        foreach (var ip in entry.AddressList)
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                yield return ip.ToString();
    }
}
