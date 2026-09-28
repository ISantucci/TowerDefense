using TMPro;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>Número o palabra que sube y se desvanece sobre el mapa (+oro, INMUNE, CALENTANDO…). Se recicla.</summary>
    [RequireComponent(typeof(TextMeshPro)), AddComponentMenu("Clash Defense/Efectos/Texto flotante")]
    public sealed class FloatingText : MonoBehaviour
    {
        TextMeshPro tmp;
        Vector3 origin;
        Color color;
        float duration, elapsed;

        void Awake() { tmp = GetComponent<TextMeshPro>(); }

        public void Play(Vector3 position, Quaternion rotation, string text, Color c, float size, float seconds)
        {
            if (tmp == null) tmp = GetComponent<TextMeshPro>();
            origin = position;
            color = c;
            duration = Mathf.Max(0.01f, seconds);
            elapsed = 0f;
            transform.SetPositionAndRotation(position, rotation);
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = c;
        }

        public bool Tick(float dt)
        {
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = origin + Vector3.up * (t * 1.2f);
            var c = color; c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            tmp.color = c;
            return t < 1f;
        }

        // "+N" sin crear un texto nuevo en cada muerte (Vaultrum: Strings por frame)
        static readonly string[] plus = new string[1001];
        public static string Plus(int n)
        {
            if (n < 0 || n >= plus.Length) return "+" + n;
            return plus[n] ?? (plus[n] = "+" + n);
        }
    }
}
