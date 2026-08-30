using UnityEngine;

/// <summary>
/// Punto de entrada del módulo de game feel. LevelBootstrap lo llama una vez por escena de juego,
/// justo después de armar el nivel. Crea el objeto "GameFeel" con todos los sistemas de feedback;
/// cada sistema se suscribe a los eventos en OnEnable y se desuscribe en OnDisable, así que
/// una recarga de escena no deja suscripciones colgadas.
/// </summary>
public static class GameFeel
{
    public const string ObjectName = "GameFeel";

    /// <summary>Raíz activa en la escena actual (el objeto "GameFeel" de la escena del nivel; null si no hay).</summary>
    public static GameFeelRoot Current { get; internal set; }

    /// <summary>
    /// Compatibilidad: si una escena no trae el objeto GameFeel, se crea uno con todos los sistemas.
    /// En las escenas de nivel el objeto existe en la jerarquía y este método sólo actualiza la referencia.
    /// </summary>
    public static void Attach(LevelController rt)
    {
        if (Current != null)
        {
            Current.Runtime = rt;
            return;
        }

        var go = new GameObject(ObjectName);
        var root = go.AddComponent<GameFeelRoot>();
        root.Runtime = rt;
        Current = root;

        go.AddComponent<ProceduralAudioPlayer>();
        go.AddComponent<FeelParticles>();
        go.AddComponent<FeelRings>();
        go.AddComponent<DamageNumbers>();
        go.AddComponent<EnemyHealthBars>();
        go.AddComponent<HitFlash>();
        go.AddComponent<DeathBurst>();
        go.AddComponent<BeamVisuals>();
        go.AddComponent<BuildFeedback>();
        go.AddComponent<CameraShake>();

        ProceduralAudio.Warmup();
    }
}



/// <summary>
/// Utilidades compartidas por los sistemas de feedback: cámara cacheada por frame, materiales
/// (Standard opaco / Fade), mallas primitivas sin collider y easings.
/// </summary>
public static class GameFeelKit
{
    static Camera cachedCam;
    static int cachedCamFrame = -1;

    /// <summary>Camera.main cacheada por frame (puede ser null si no hay cámara con tag MainCamera).</summary>
    public static Camera MainCamera
    {
        get
        {
            if (cachedCamFrame != Time.frameCount || cachedCam == null)
            {
                cachedCamFrame = Time.frameCount;
                cachedCam = Camera.main;
            }
            return cachedCam;
        }
    }

    /// <summary>Rotación de la cámara principal; devuelve false si no hay cámara.</summary>
    public static bool TryGetCameraRotation(out Quaternion rot)
    {
        var cam = MainCamera;
        if (cam == null)
        {
            rot = Quaternion.identity;
            return false;
        }
        rot = cam.transform.rotation;
        return true;
    }

    // ───────────────────────── shaders / materiales ─────────────────────────

    public static Shader StandardShader()
    {
        var sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Diffuse");
        return sh;
    }

    public static Shader LineShader()
    {
        var sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = StandardShader();
        return sh;
    }

    /// <summary>Material Standard opaco, mate. Si emission &gt; 0 habilita _EMISSION (el color se puede pisar por MaterialPropertyBlock).</summary>
    public static Material MakeOpaque(Color color, float emission)
    {
        var sh = StandardShader();
        if (sh == null) return null;
        var m = new Material(sh);
        m.name = "GameFeel_Opaque";
        if (m.HasProperty("_Color")) m.color = color;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * emission);
        }
        return m;
    }

    /// <summary>Material Standard en modo Fade (alfa afecta a todo el objeto), sin escribir profundidad.</summary>
    public static Material MakeTransparent(Color color)
    {
        var sh = StandardShader();
        if (sh == null) return null;
        var m = new Material(sh);
        m.name = "GameFeel_Fade";
        if (m.HasProperty("_Color")) m.color = color;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
        if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 2f);
        if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 0.4f);
        m.renderQueue = 3000;
        return m;
    }

    /// <summary>Material para LineRenderers (Sprites/Default usa el color por vértice).</summary>
    public static Material MakeLineMaterial()
    {
        var sh = LineShader();
        if (sh == null) return null;
        var m = new Material(sh);
        m.name = "GameFeel_Line";
        if (m.HasProperty("_Color")) m.color = Color.white;
        return m;
    }

    public static void SafeDestroy(Object o)
    {
        if (o != null) Object.Destroy(o);
    }

    // ───────────────────────── mallas primitivas ─────────────────────────

    static Mesh cubeMesh;
    static Mesh cylinderMesh;

    public static Mesh CubeMesh
    {
        get
        {
            if (cubeMesh == null) cubeMesh = LoadPrimitiveMesh("Cube.fbx", PrimitiveType.Cube);
            return cubeMesh;
        }
    }

    public static Mesh CylinderMesh
    {
        get
        {
            if (cylinderMesh == null) cylinderMesh = LoadPrimitiveMesh("Cylinder.fbx", PrimitiveType.Cylinder);
            return cylinderMesh;
        }
    }

    static Mesh LoadPrimitiveMesh(string builtinName, PrimitiveType fallback)
    {
        Mesh mesh = null;
        try
        {
            mesh = Resources.GetBuiltinResource<Mesh>(builtinName);
        }
        catch (System.Exception)
        {
            mesh = null;
        }
        if (mesh != null) return mesh;

        // Fallback: crear un primitivo, robarle la malla compartida y destruirlo.
        var temp = GameObject.CreatePrimitive(fallback);
        var mf = temp.GetComponent<MeshFilter>();
        if (mf != null) mesh = mf.sharedMesh;
        Object.Destroy(temp);
        return mesh;
    }

    /// <summary>GameObject con MeshFilter + MeshRenderer (sin collider, sin sombras).</summary>
    public static GameObject MakeMeshObject(string name, Mesh mesh, Material material, Transform parent)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    // ───────────────────────── easings ─────────────────────────

    public static float EaseOutQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    public static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        return 1f - u * u * u;
    }

    public static float EaseInQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }
}
