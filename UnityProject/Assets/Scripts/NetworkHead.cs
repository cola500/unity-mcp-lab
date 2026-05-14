using Unity.Netcode;
using UnityEngine;

public class NetworkHead : NetworkBehaviour
{
    [SerializeField] private GameObject visual;
    private Transform _headSource;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            if (visual != null) visual.SetActive(false);
            var cam = Camera.main;
            if (cam != null) _headSource = cam.transform;
        }
    }

    void LateUpdate()
    {
        if (!IsOwner || _headSource == null) return;
        transform.SetPositionAndRotation(_headSource.position, _headSource.rotation);
    }
}
