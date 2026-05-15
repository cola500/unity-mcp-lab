using UnityEngine;

public class PresenceBreath : MonoBehaviour
{
    [SerializeField, Range(0f, 0.1f)] private float amplitude = 0.02f;
    [SerializeField, Range(0.5f, 10f)] private float period = 4f;

    private Vector3 _baseScale;
    private float _phase;

    void OnEnable()
    {
        _baseScale = transform.localScale;
        _phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        if (period <= 0f) return;
        float t = (Time.time / period) * Mathf.PI * 2f + _phase;
        float breath = Mathf.Sin(t) * amplitude;
        var s = _baseScale;
        s.y += breath;
        transform.localScale = s;
    }
}
