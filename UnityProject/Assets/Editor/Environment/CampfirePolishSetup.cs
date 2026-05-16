using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class CampfirePolishSetup
{
    private const string FlameMaterialPath = "Assets/Materials/FlameMaterial.mat";
    private const string EmberMaterialPath = "Assets/Materials/EmberParticleMaterial.mat";
    private const string EmberTexturePath = "Assets/Materials/EmberParticleTex.png";

    [MenuItem("Tools/Quest Setup/Apply Campfire Polish")]
    public static void Apply()
    {
        ConfigureFlameMaterial();
        var flame = GameObject.Find("Flame");
        if (flame == null)
        {
            Debug.LogWarning("[CampfirePolish] Flame GameObject not found in active scene.");
            return;
        }
        ConfigureFlameRenderer(flame);
        ConfigureEmbers(flame);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[CampfirePolish] Applied (emission on, no shadows from flame, embers ready).");
    }

    private static void ConfigureFlameMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
        if (mat == null)
        {
            Debug.LogWarning($"[CampfirePolish] {FlameMaterialPath} not found.");
            return;
        }

        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.SetColor("_EmissionColor", new Color(1.0f, 0.5f, 0.18f) * 1.6f);
        EditorUtility.SetDirty(mat);
    }

    private static void ConfigureFlameRenderer(GameObject flame)
    {
        var mr = flame.GetComponent<MeshRenderer>();
        if (mr == null) return;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        EditorUtility.SetDirty(mr);
    }

    private static void ConfigureEmbers(GameObject flame)
    {
        var existing = flame.transform.Find("Embers");
        GameObject embers = existing != null ? existing.gameObject : new GameObject("Embers");
        if (existing == null)
        {
            embers.transform.SetParent(flame.transform, false);
            embers.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        }

        var ps = embers.GetComponent<ParticleSystem>();
        if (ps == null) ps = embers.AddComponent<ParticleSystem>();
        ConfigureEmberParticleSystem(ps, flame.transform);

        var psr = embers.GetComponent<ParticleSystemRenderer>();
        ConfigureEmberRenderer(psr);

        EditorUtility.SetDirty(embers);
    }

    private static void ConfigureEmberParticleSystem(ParticleSystem ps, Transform flame)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 3.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
        main.startColor = new Color(1.0f, 0.75f, 0.30f, 0.85f);
        main.gravityModifier = -0.05f;
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 4.5f;
        emission.rateOverDistance = 0f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.12f;
        shape.position = Vector3.zero;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.15f), 0.6f),
                new GradientColorKey(new Color(0.4f, 0.10f, 0.0f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.15f),
                new GradientAlphaKey(0.55f, 0.7f),
                new GradientAlphaKey(0f, 1f),
            }
        );
        col.color = grad;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        vel.y = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        vel.space = ParticleSystemSimulationSpace.Local;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.7f, 0.6f),
            new Keyframe(1f, 0.1f)
        );
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.08f;
        noise.frequency = 0.6f;
        noise.scrollSpeed = 0.4f;

        ps.Play(true);
    }

    private static void ConfigureEmberRenderer(ParticleSystemRenderer psr)
    {
        if (psr == null) return;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(EmberMaterialPath);
        if (mat == null)
        {
            var shader = Shader.Find("Mobile/Particles/Additive")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Sprites/Default");
            mat = new Material(shader) { name = "EmberParticleMaterial" };
            mat.color = new Color(1f, 0.75f, 0.30f, 1f);
            AssetDatabase.CreateAsset(mat, EmberMaterialPath);
        }

        if (mat.mainTexture == null)
        {
            mat.mainTexture = GetOrCreateEmberTexture();
            EditorUtility.SetDirty(mat);
        }

        psr.material = mat;
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.alignment = ParticleSystemRenderSpace.View;
        psr.sortingFudge = -0.1f;
    }

    private static Texture2D GetOrCreateEmberTexture()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(EmberTexturePath);
        if (existing != null) return existing;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDist = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = Mathf.Clamp01(1f - d / maxDist);
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        System.IO.File.WriteAllBytes(EmberTexturePath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(EmberTexturePath);

        var importer = AssetImporter.GetAtPath(EmberTexturePath) as TextureImporter;
        if (importer != null)
        {
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(EmberTexturePath);
    }
}
