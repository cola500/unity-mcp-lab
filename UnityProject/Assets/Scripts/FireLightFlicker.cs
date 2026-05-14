using UnityEngine;

[RequireComponent(typeof(Light))]
public class FireLightFlicker : MonoBehaviour
{
    [SerializeField] private float baseIntensity = 3f;
    [SerializeField] private float flickerAmount = 0.4f;
    [SerializeField] private float flickerSpeed = 4f;

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
        _light.intensity = baseIntensity + (noise - 0.5f) * 2f * flickerAmount;
    }
}
