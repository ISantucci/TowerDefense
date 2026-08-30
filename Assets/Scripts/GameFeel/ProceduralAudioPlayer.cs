using System;
using UnityEngine;

/// <summary>
/// Pool de 12 AudioSources 2D con round-robin y throttle por Sfx (máx. uno cada 40 ms).
/// Lo crea GameFeel.Attach; si algo reproduce un sonido antes (menús), se crea uno suelto.
/// </summary>
public class ProceduralAudioPlayer : MonoBehaviour
{
    public const string StandaloneName = "GameFeel.Audio";
    public const int PoolSize = 12;
    public const float MinIntervalSameSfx = 0.04f;

    public static ProceduralAudioPlayer Instance { get; private set; }

    AudioSource[] sources;
    float[] lastPlayTime;
    int cursor;

    public static ProceduralAudioPlayer CreateStandalone()
    {
        if (!Application.isPlaying) return null;
        var go = new GameObject(StandaloneName);
        return go.AddComponent<ProceduralAudioPlayer>();   // Awake/OnEnable corren acá y setean Instance
    }

    void Awake()
    {
        sources = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.dopplerLevel = 0f;
            src.ignoreListenerPause = true;
            src.priority = 128;
            sources[i] = src;
        }

        int n = Enum.GetValues(typeof(Sfx)).Length;
        lastPlayTime = new float[n];
        for (int i = 0; i < n; i++) lastPlayTime[i] = -10f;

        // Sin AudioListener no se escucha nada: si la escena no tiene uno, lo ponemos acá.
        if (FindFirstObjectByType<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();
    }

    void OnEnable()
    {
        Instance = this;
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Reproduce un clip con volumen final (ya multiplicado por el maestro) y pitch levemente aleatorio.</summary>
    public void PlayClip(Sfx sfx, AudioClip clip, float volume)
    {
        if (clip == null || sources == null || volume <= 0f) return;

        int idx = (int)sfx;
        float now = Time.unscaledTime;
        if (idx >= 0 && idx < lastPlayTime.Length)
        {
            if (now - lastPlayTime[idx] < MinIntervalSameSfx) return;
            lastPlayTime[idx] = now;
        }

        var src = PickSource();
        if (src == null) return;

        src.Stop();
        src.clip = clip;
        src.volume = Mathf.Clamp01(volume);
        src.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
        src.Play();
    }

    AudioSource PickSource()
    {
        // Primero una fuente libre (a partir del cursor); si están todas ocupadas, round-robin puro.
        for (int k = 0; k < PoolSize; k++)
        {
            int i = (cursor + k) % PoolSize;
            var s = sources[i];
            if (s != null && !s.isPlaying)
            {
                cursor = (i + 1) % PoolSize;
                return s;
            }
        }
        var chosen = sources[cursor];
        cursor = (cursor + 1) % PoolSize;
        return chosen;
    }
}
