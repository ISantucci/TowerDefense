using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Línea de ataque: el rayo quebrado de la Eléctrica (Doc 04 §6.3, un instante) y el rayo continuo de la Infernal, que se
    /// ensancha y cambia de color por etapa (Doc 04 §18: el calentamiento se ve). Se recicla.
    /// </summary>
    [RequireComponent(typeof(LineRenderer)), AddComponentMenu("Clash Defense/Efectos/Línea")]
    public sealed class LineFx : MonoBehaviour
    {
        LineRenderer line;
        float duration, elapsed;
        Color color;
        bool persistent;

        public LineRenderer Line => line != null ? line : (line = GetComponent<LineRenderer>());

        public void Lightning(Vector3 from, Vector3 to, int link, Color c)
        {
            persistent = false; duration = 0.16f; elapsed = 0f; color = c;
            var lr = Line;
            lr.enabled = true;
            lr.widthMultiplier = link <= 1 ? 0.14f : 0.1f;
            const int n = 7;
            lr.positionCount = n;
            var side = Vector3.Cross((to - from).normalized, Vector3.up);
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                var p = Vector3.Lerp(from, to, k);
                if (i > 0 && i < n - 1) p += side * Random.Range(-0.25f, 0.25f) + Vector3.up * Random.Range(-0.15f, 0.15f);
                lr.SetPosition(i, p);
            }
            lr.startColor = lr.endColor = c;
        }

        /// <summary>Rayo infernal: lo actualiza WorldView cada cuadro mientras la torre dispara.</summary>
        public void Beam(Vector3 from, Vector3 to, float width, Color c)
        {
            persistent = true;
            var lr = Line;
            lr.enabled = true;
            lr.positionCount = 2;
            lr.SetPosition(0, from); lr.SetPosition(1, to);
            lr.widthMultiplier = width;
            lr.startColor = c;
            lr.endColor = Color.Lerp(c, Color.white, 0.4f);
        }

        public void Hide() { Line.enabled = false; }

        public bool Tick(float dt)
        {
            if (persistent) return true;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / duration);
            var c = color; c.a = 1f - t; Line.startColor = Line.endColor = c;
            return t < 1f;
        }
    }
}
