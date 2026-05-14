using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class NightAtmosphere : MonoBehaviour
{
    [SerializeField] private Color ambientColor = new Color(0.03f, 0.04f, 0.08f, 1f);
    [SerializeField] private bool clearSkybox = true;

    void OnEnable()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    void Apply()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        if (clearSkybox) RenderSettings.skybox = null;
    }
}
