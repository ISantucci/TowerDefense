using System;
using UnityEngine;

/// <summary>Efectos de sonido del juego. Todos se sintetizan en runtime (no hay assets de audio).</summary>
public enum Sfx
{
    Shoot, ShootHeavy, Beam, Chain, Hit, EnemyDeath, EnemyDeathBig, EnemyLeak,
    Build, Sell, Upgrade, Select, Reject, WaveStart, WaveClear, Win, Lose, Click, Coin, Push
}

/// <summary>
/// Audio procedural: sintetiza cada Sfx una sola vez (caché estática) con AudioClip.Create y lo reproduce
/// a través de un pool de AudioSources 2D que vive en el objeto GameFeel. Fire-and-forget.
/// </summary>
public static class ProceduralAudio
{
    public const string VolumePrefKey = "td.volume";
    public const float DefaultVolume = 0.7f;

    static readonly int SfxCount = Enum.GetValues(typeof(Sfx)).Length;
    static AudioClip[] clips;
    static float masterVolume = -1f;

    /// <summary>Volumen maestro (0..1), persistido en PlayerPrefs "td.volume". Por defecto 0.7.</summary>
    public static float MasterVolume
    {
        get
        {
            if (masterVolume < 0f)
                masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePrefKey, DefaultVolume));
            return masterVolume;
        }
        set
        {
            masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumePrefKey, masterVolume);
            PlayerPrefs.Save();
        }
    }

    public static void Play(Sfx sfx)
    {
        Play(sfx, 1f);
    }

    public static void Play(Sfx sfx, float volume)
    {
        if (volume <= 0f) return;
        var clip = GetClip(sfx);
        if (clip == null) return;

        var player = ProceduralAudioPlayer.Instance;
        if (player == null) player = ProceduralAudioPlayer.CreateStandalone();
        if (player == null) return;

        player.PlayClip(sfx, clip, volume * MasterVolume);
    }

    /// <summary>
    /// Reproduce en un objeto propio que sobrevive al cambio de escena (para Win/Lose, que se disparan
    /// justo antes de cargar otra escena). Se autodestruye al terminar el clip.
    /// </summary>
    public static void PlayDetached(Sfx sfx, float volume)
    {
        if (volume <= 0f || !Application.isPlaying) return;
        var clip = GetClip(sfx);
        if (clip == null) return;

        var go = new GameObject("GameFeel.OneShot." + sfx);
        UnityEngine.Object.DontDestroyOnLoad(go);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = true;
        src.clip = clip;
        src.volume = Mathf.Clamp01(volume * MasterVolume);
        src.pitch = 1f;
        src.Play();
        UnityEngine.Object.Destroy(go, clip.length + 0.25f);
    }

    /// <summary>Sintetiza todos los clips ahora (evita un tirón en el primer disparo).</summary>
    public static void Warmup()
    {
        for (int i = 0; i < SfxCount; i++) GetClip((Sfx)i);
    }

    public static AudioClip GetClip(Sfx sfx)
    {
        if (clips == null) clips = new AudioClip[SfxCount];
        int i = (int)sfx;
        if (i < 0 || i >= clips.Length) return null;
        if (clips[i] == null) clips[i] = FeelSynth.Build(sfx);
        return clips[i];
    }
}



/// <summary>
/// Mini sintetizador: osciladores (seno/cuadrada/triángulo/sierra/ruido) con envolvente ataque/decay
/// y barrido de frecuencia, sumados en un buffer mono a 44100 Hz. Determinista (semilla fija).
/// </summary>
public static class FeelSynth
{
    public const int Rate = 44100;

    public enum Wave { Sine, Square, Triangle, Saw }

    static System.Random rng = new System.Random(1337);

    public static AudioClip Build(Sfx sfx)
    {
        rng = new System.Random(1337 + (int)sfx);
        float[] buf;

        switch (sfx)
        {
            case Sfx.Shoot:
                buf = Buffer(0.10f);
                Tone(buf, Wave.Square, 0f, 0.09f, 1100f, 380f, 0.35f, 0.002f, 2.5f);
                Tone(buf, Wave.Sine, 0f, 0.06f, 700f, 250f, 0.30f, 0.001f, 2.0f);
                Noise(buf, 0f, 0.03f, 0.25f, 0.001f, 3f, 0.6f);
                break;

            case Sfx.ShootHeavy:
                buf = Buffer(0.18f);
                Tone(buf, Wave.Saw, 0f, 0.16f, 300f, 70f, 0.40f, 0.003f, 2.0f);
                Tone(buf, Wave.Sine, 0f, 0.12f, 160f, 45f, 0.50f, 0.002f, 1.5f);
                Noise(buf, 0f, 0.10f, 0.50f, 0.002f, 2.5f, 0.25f);
                break;

            case Sfx.Beam:
                buf = Buffer(0.14f);
                Tone(buf, Wave.Sine, 0f, 0.14f, 210f, 190f, 0.22f, 0.02f, 1.2f);
                Tone(buf, Wave.Triangle, 0f, 0.14f, 420f, 380f, 0.14f, 0.02f, 1.2f);
                Tone(buf, Wave.Sine, 0f, 0.14f, 1260f, 1140f, 0.05f, 0.02f, 1.5f);
                break;

            case Sfx.Chain:
                buf = Buffer(0.12f);
                Noise(buf, 0f, 0.10f, 0.50f, 0.001f, 3f, 0.9f);
                Tone(buf, Wave.Square, 0f, 0.09f, 1800f, 600f, 0.25f, 0.001f, 2.5f);
                Tone(buf, Wave.Square, 0.03f, 0.06f, 2400f, 900f, 0.15f, 0.001f, 2.5f);
                break;

            case Sfx.Hit:
                buf = Buffer(0.07f);
                Noise(buf, 0f, 0.06f, 0.60f, 0.001f, 3f, 0.35f);
                Tone(buf, Wave.Sine, 0f, 0.05f, 260f, 120f, 0.40f, 0.001f, 2.0f);
                break;

            case Sfx.EnemyDeath:
                buf = Buffer(0.26f);
                Noise(buf, 0f, 0.22f, 0.55f, 0.002f, 2.2f, 0.3f);
                Tone(buf, Wave.Sine, 0f, 0.20f, 420f, 110f, 0.45f, 0.002f, 1.8f);
                Tone(buf, Wave.Square, 0f, 0.08f, 900f, 300f, 0.12f, 0.001f, 2.0f);
                break;

            case Sfx.EnemyDeathBig:
                buf = Buffer(0.55f);
                Noise(buf, 0f, 0.50f, 0.70f, 0.005f, 1.8f, 0.12f);
                Tone(buf, Wave.Sine, 0f, 0.45f, 110f, 38f, 0.60f, 0.003f, 1.5f);
                Tone(buf, Wave.Saw, 0f, 0.20f, 240f, 60f, 0.25f, 0.002f, 2.0f);
                Noise(buf, 0.02f, 0.12f, 0.40f, 0.001f, 2.5f, 0.6f);
                break;

            case Sfx.EnemyLeak:
                buf = Buffer(0.45f);
                Tone(buf, Wave.Square, 0f, 0.20f, 330f, 300f, 0.28f, 0.01f, 1.2f);
                Tone(buf, Wave.Square, 0.20f, 0.25f, 220f, 170f, 0.28f, 0.01f, 1.5f);
                Tone(buf, Wave.Sine, 0f, 0.45f, 165f, 85f, 0.20f, 0.01f, 1.2f);
                break;

            case Sfx.Build:
                buf = Buffer(0.22f);
                Noise(buf, 0f, 0.08f, 0.45f, 0.002f, 2.5f, 0.25f);
                Tone(buf, Wave.Triangle, 0f, 0.10f, 280f, 260f, 0.35f, 0.003f, 1.8f);
                Tone(buf, Wave.Triangle, 0.10f, 0.12f, 420f, 400f, 0.35f, 0.003f, 1.8f);
                Tone(buf, Wave.Sine, 0.10f, 0.05f, 140f, 100f, 0.30f, 0.001f, 2.0f);
                break;

            case Sfx.Sell:
                buf = Buffer(0.24f);
                Tone(buf, Wave.Sine, 0f, 0.10f, 1400f, 1300f, 0.30f, 0.002f, 2.0f);
                Tone(buf, Wave.Sine, 0.08f, 0.16f, 1000f, 600f, 0.30f, 0.002f, 2.0f);
                Tone(buf, Wave.Triangle, 0f, 0.20f, 700f, 350f, 0.15f, 0.002f, 2.0f);
                break;

            case Sfx.Upgrade:
                buf = Buffer(0.36f);
                Tone(buf, Wave.Triangle, 0f, 0.12f, 440f, 440f, 0.30f, 0.005f, 1.8f);
                Tone(buf, Wave.Triangle, 0.10f, 0.12f, 554f, 554f, 0.30f, 0.005f, 1.8f);
                Tone(buf, Wave.Triangle, 0.20f, 0.16f, 659f, 659f, 0.30f, 0.005f, 1.8f);
                Tone(buf, Wave.Sine, 0.20f, 0.16f, 1318f, 1318f, 0.08f, 0.005f, 2.0f);
                break;

            case Sfx.Select:
                buf = Buffer(0.05f);
                Tone(buf, Wave.Sine, 0f, 0.045f, 1000f, 950f, 0.25f, 0.002f, 2.5f);
                break;

            case Sfx.Reject:
                buf = Buffer(0.22f);
                Tone(buf, Wave.Square, 0f, 0.08f, 150f, 140f, 0.30f, 0.003f, 1.2f);
                Tone(buf, Wave.Square, 0.11f, 0.10f, 130f, 110f, 0.30f, 0.003f, 1.2f);
                break;

            case Sfx.WaveStart:
                buf = Buffer(0.65f);
                // Bocina de dos notas (sierra + cuadrada, un poco desafinadas para engordar).
                Tone(buf, Wave.Saw, 0f, 0.25f, 220f, 220f, 0.22f, 0.03f, 0.8f);
                Tone(buf, Wave.Saw, 0f, 0.25f, 223f, 223f, 0.12f, 0.03f, 0.8f);
                Tone(buf, Wave.Square, 0f, 0.25f, 110f, 110f, 0.10f, 0.03f, 0.8f);
                Tone(buf, Wave.Saw, 0.25f, 0.38f, 293.66f, 293.66f, 0.22f, 0.03f, 1.2f);
                Tone(buf, Wave.Saw, 0.25f, 0.38f, 297f, 297f, 0.12f, 0.03f, 1.2f);
                Tone(buf, Wave.Square, 0.25f, 0.38f, 146.83f, 146.83f, 0.10f, 0.03f, 1.2f);
                break;

            case Sfx.WaveClear:
                buf = Buffer(0.58f);
                Arp(buf, Wave.Triangle, 0f, 0.10f, 0.14f, 0.28f, 523.25f, 659.25f, 783.99f, 1046.5f);
                Tone(buf, Wave.Triangle, 0.30f, 0.26f, 1046.5f, 1046.5f, 0.28f, 0.005f, 1.8f);
                Tone(buf, Wave.Sine, 0.30f, 0.26f, 523.25f, 523.25f, 0.12f, 0.005f, 1.8f);
                break;

            case Sfx.Win:
                buf = Buffer(1.30f);
                Arp(buf, Wave.Triangle, 0f, 0.11f, 0.16f, 0.26f, 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f, 1568f);
                // acorde final sostenido
                Tone(buf, Wave.Triangle, 0.66f, 0.62f, 1046.5f, 1046.5f, 0.20f, 0.02f, 1.5f);
                Tone(buf, Wave.Triangle, 0.66f, 0.62f, 1318.5f, 1318.5f, 0.16f, 0.02f, 1.5f);
                Tone(buf, Wave.Triangle, 0.66f, 0.62f, 1568f, 1568f, 0.14f, 0.02f, 1.5f);
                Tone(buf, Wave.Sine, 0.66f, 0.62f, 523.25f, 523.25f, 0.18f, 0.02f, 1.5f);
                break;

            case Sfx.Lose:
                buf = Buffer(1.30f);
                Tone(buf, Wave.Square, 0f, 0.28f, 440f, 440f, 0.26f, 0.01f, 1.4f);
                Tone(buf, Wave.Square, 0.26f, 0.28f, 369.99f, 369.99f, 0.26f, 0.01f, 1.4f);
                Tone(buf, Wave.Square, 0.52f, 0.28f, 311.13f, 311.13f, 0.26f, 0.01f, 1.4f);
                Tone(buf, Wave.Square, 0.78f, 0.50f, 220f, 200f, 0.26f, 0.01f, 1.4f);
                Tone(buf, Wave.Sine, 0f, 0.28f, 220f, 220f, 0.15f, 0.01f, 1.4f);
                Tone(buf, Wave.Sine, 0.26f, 0.28f, 185f, 185f, 0.15f, 0.01f, 1.4f);
                Tone(buf, Wave.Sine, 0.52f, 0.28f, 155.56f, 155.56f, 0.15f, 0.01f, 1.4f);
                Tone(buf, Wave.Sine, 0.78f, 0.50f, 110f, 100f, 0.15f, 0.01f, 1.4f);
                break;

            case Sfx.Click:
                buf = Buffer(0.035f);
                Tone(buf, Wave.Sine, 0f, 0.03f, 1600f, 1200f, 0.22f, 0.001f, 3f);
                Noise(buf, 0f, 0.012f, 0.15f, 0.0005f, 3f, 0.9f);
                break;

            case Sfx.Coin:
                buf = Buffer(0.16f);
                Tone(buf, Wave.Sine, 0f, 0.06f, 1760f, 1760f, 0.30f, 0.001f, 2.2f);
                Tone(buf, Wave.Sine, 0.05f, 0.11f, 2349f, 2349f, 0.30f, 0.001f, 2.5f);
                Tone(buf, Wave.Sine, 0.05f, 0.11f, 4698f, 4698f, 0.08f, 0.001f, 2.5f);
                break;

            case Sfx.Push:
                buf = Buffer(0.20f);
                Noise(buf, 0f, 0.18f, 0.45f, 0.04f, 1.8f, 0.2f);
                Tone(buf, Wave.Sine, 0f, 0.15f, 170f, 70f, 0.35f, 0.01f, 1.8f);
                break;

            default:
                buf = Buffer(0.05f);
                Tone(buf, Wave.Sine, 0f, 0.045f, 880f, 880f, 0.25f, 0.002f, 2f);
                break;
        }

        Finish(buf, 0.85f);

        var clip = AudioClip.Create("Sfx_" + sfx, buf.Length, 1, Rate, false);
        clip.SetData(buf, 0);
        return clip;
    }

    // ───────────────────────── bloques ─────────────────────────

    static float[] Buffer(float seconds)
    {
        int n = Mathf.Max(16, Mathf.CeilToInt(seconds * Rate));
        return new float[n];
    }

    /// <summary>Suma un tono con barrido exponencial f0→f1 y envolvente ataque lineal / decay potencial.</summary>
    public static void Tone(float[] buf, Wave wave, float start, float dur, float f0, float f1, float amp, float attack, float decayPow)
    {
        if (buf == null || dur <= 0f) return;
        int s0 = Mathf.Max(0, (int)(start * Rate));
        int n = (int)(dur * Rate);
        if (n <= 0) return;

        double phase = 0.0;
        bool expo = f0 > 0f && f1 > 0f;
        float ratio = expo ? f1 / f0 : 1f;

        for (int i = 0; i < n; i++)
        {
            int idx = s0 + i;
            if (idx >= buf.Length) break;

            float t = i / (float)Rate;
            float u = i / (float)n;
            float f = expo ? f0 * Mathf.Pow(ratio, u) : f0 + (f1 - f0) * u;

            phase += f / Rate;
            if (phase >= 1.0) phase -= 1.0;

            float v = Osc(wave, (float)phase);
            buf[idx] += v * amp * Env(t, dur, attack, decayPow);
        }
    }

    /// <summary>Ráfaga de ruido blanco con lowpass de un polo (lowpass 1 = sin filtro, 0.1 = muy grave).</summary>
    public static void Noise(float[] buf, float start, float dur, float amp, float attack, float decayPow, float lowpass)
    {
        if (buf == null || dur <= 0f) return;
        int s0 = Mathf.Max(0, (int)(start * Rate));
        int n = (int)(dur * Rate);
        if (n <= 0) return;

        float alpha = Mathf.Clamp(lowpass, 0.01f, 1f);
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            int idx = s0 + i;
            if (idx >= buf.Length) break;

            float t = i / (float)Rate;
            float x = (float)(rng.NextDouble() * 2.0 - 1.0);
            y += alpha * (x - y);
            buf[idx] += y * amp * Env(t, dur, attack, decayPow);
        }
    }

    /// <summary>Arpegio: notas sucesivas espaciadas 'spacing' segundos, cada una de 'noteDur'.</summary>
    static void Arp(float[] buf, Wave wave, float start, float spacing, float noteDur, float amp, params float[] freqs)
    {
        if (freqs == null) return;
        for (int i = 0; i < freqs.Length; i++)
        {
            float t0 = start + spacing * i;
            Tone(buf, wave, t0, noteDur, freqs[i], freqs[i], amp, 0.004f, 1.8f);
            Tone(buf, Wave.Sine, t0, noteDur, freqs[i] * 0.5f, freqs[i] * 0.5f, amp * 0.35f, 0.004f, 1.8f);
        }
    }

    static float Osc(Wave w, float p)
    {
        switch (w)
        {
            case Wave.Sine: return Mathf.Sin(p * 6.2831853f);
            case Wave.Square: return p < 0.5f ? 1f : -1f;
            case Wave.Triangle: return 1f - 4f * Mathf.Abs(p - 0.5f);
            case Wave.Saw: return 2f * p - 1f;
        }
        return 0f;
    }

    static float Env(float t, float dur, float attack, float decayPow)
    {
        if (attack > 0f && t < attack) return t / attack;
        float d = dur - attack;
        if (d <= 0f) return 1f;
        float r = 1f - (t - attack) / d;
        if (r <= 0f) return 0f;
        if (r >= 1f) return 1f;
        return Mathf.Pow(r, decayPow);
    }

    /// <summary>Normaliza al pico pedido y aplica un fundido de salida cortito para evitar clics.</summary>
    static void Finish(float[] buf, float peak)
    {
        float max = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float a = Mathf.Abs(buf[i]);
            if (a > max) max = a;
        }
        if (max > 0.00001f)
        {
            float g = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        int fade = Mathf.Min(buf.Length, Rate / 400);   // ~2.5 ms
        for (int i = 0; i < fade; i++)
        {
            int idx = buf.Length - 1 - i;
            buf[idx] *= i / (float)fade;
        }
    }
}
