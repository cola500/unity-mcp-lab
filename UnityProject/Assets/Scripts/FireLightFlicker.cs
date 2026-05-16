using UnityEngine;

[RequireComponent(typeof(Light))]
public class FireLightFlicker : MonoBehaviour
{
    [SerializeField] private float baseIntensity = 4f;
    [SerializeField] private float flickerAmount = 0.6f;
    [SerializeField] private float flickerSpeed = 4f;
    [SerializeField] private Color dimColor = new Color(1.0f, 0.42f, 0.14f);
    [SerializeField] private Color brightColor = new Color(1.0f, 0.65f, 0.28f);

    private Light _light;
    private float _noiseOffset;

    void Awake()
    {
        _light = GetComponent<Light>();
        _noiseOffset = Random.value * 100f;
    }

    void Update()
    {
        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed + _noiseOffset, 0f);
        float t = Mathf.Clamp01(noise);
        _light.intensity = baseIntensity + (t - 0.5f) * 2f * flickerAmount;
        _light.color = Color.Lerp(dimColor, brightColor, t);
    }
}
