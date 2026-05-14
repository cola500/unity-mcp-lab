using UnityEngine;
using UnityEngine.XR;

public class XRControllerInputFeedback : MonoBehaviour
{
    [SerializeField] private XRNode node = XRNode.RightHand;
    [SerializeField] private Transform visualTarget;
    [SerializeField, Range(1f, 2f)] private float pressedScaleMultiplier = 1.15f;
    [SerializeField] private float smoothing = 12f;
    [SerializeField, Range(0f, 1f)] private float triggerThreshold = 0.3f;

    private Vector3 _baseScale = Vector3.one;
    private bool _initialized;

    void OnEnable()
    {
        if (visualTarget == null && transform.childCount > 0)
            visualTarget = transform.GetChild(0);
        if (visualTarget != null)
        {
            _baseScale = visualTarget.localScale;
            _initialized = true;
        }
    }

    void Update()
    {
        if (!_initialized || visualTarget == null) return;

        var device = InputDevices.GetDeviceAtXRNode(node);
        bool pressed = false;
        if (device.isValid)
        {
            if (device.TryGetFeatureValue(CommonUsages.trigger, out float t))
                pressed = t > triggerThreshold;
            else if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool b))
                pressed = b;
        }

        Vector3 target = pressed ? _baseScale * pressedScaleMultiplier : _baseScale;
        visualTarget.localScale = Vector3.Lerp(visualTarget.localScale, target, smoothing * Time.deltaTime);
    }
}
