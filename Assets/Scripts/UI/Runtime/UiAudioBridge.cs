using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Acceso tolerante al volumen maestro: usa ProceduralAudio.MasterVolume (otro módulo) si existe,
/// y si no cae en AudioListener.volume. Persiste en PlayerPrefs.
/// </summary>
public static class UiAudioBridge
{
    const string PrefKey = "td.masterVolume";

    static PropertyInfo masterVolume;
    static bool searched;

    static PropertyInfo Property
    {
        get
        {
            if (!searched)
            {
                searched = true;
                masterVolume = Find();
            }
            return masterVolume;
        }
    }

    static PropertyInfo Find()
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                if (asm == null) continue;
                Type t = null;
                try { t = asm.GetType("ProceduralAudio", false); }
                catch (Exception) { t = null; }
                if (t == null) continue;

                var p = t.GetProperty("MasterVolume", BindingFlags.Public | BindingFlags.Static);
                if (p != null && p.PropertyType == typeof(float) && p.CanRead && p.CanWrite)
                    return p;
            }
        }
        catch (Exception) { }
        return null;
    }

    public static float Volume
    {
        get
        {
            var p = Property;
            if (p != null)
            {
                try { return Mathf.Clamp01((float)p.GetValue(null, null)); }
                catch (Exception) { }
            }
            return Mathf.Clamp01(AudioListener.volume);
        }
        set
        {
            float v = Mathf.Clamp01(value);
            bool applied = false;
            var p = Property;
            if (p != null)
            {
                try { p.SetValue(null, v, null); applied = true; }
                catch (Exception) { applied = false; }
            }
            if (!applied) AudioListener.volume = v;
            PlayerPrefs.SetFloat(PrefKey, v);
        }
    }

    /// <summary>Aplica el volumen guardado (si hay). Llamar al entrar a cada escena con UI.</summary>
    public static void ApplySaved()
    {
        if (!PlayerPrefs.HasKey(PrefKey)) return;
        Volume = PlayerPrefs.GetFloat(PrefKey, 1f);
    }
}
