using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Anillo en el piso con borde oscuro debajo (UXS-002.4, Ley 2: el color solo no despega del terreno). Sirve para los
    /// anillos fijos (alcance, selección, tutorial) y para los efectos que duran un instante (explosión de área, pulso de
    /// mejora, punto de impacto del Mortero). Se recicla.
    /// </summary>
    [AddComponentMenu("Clash Defense/Efectos/Anillo")]
    public sealed class RingFx : MonoBehaviour
    {
        [SerializeField] internal LineRenderer ring;
        [Tooltip("Borde oscuro debajo del anillo. Opcional.")]
        [SerializeField] internal LineRenderer under;

        enum Mode { Static, Blast, Pulse, Impact }
        Mode mode;
        float duration, elapsed, radius, y;
        Color color, edge;

        public void ShowStatic(Vector3 position, float r, Color c, float width, bool withEdge, Color edgeColor, float edgeWidth, float height = 0.07f)
        {
            mode = Mode.Static;
            transform.position = position;
            Setup(c, width, withEdge, edgeColor, edgeWidth);
            SetRadius(r, height);
        }

        public void SetColor(Color c) { if (ring != null) ring.startColor = ring.endColor = c; }

        public void SetRadius(float r, float height = 0.07f)
        {
            radius = r; y = height;
            Circle(ring, r, height);
            if (under != null && under.gameObject.activeSelf) Circle(under, r, height - 0.01f);
        }

        /// <summary>Onda que se abre desde el centro (explosión de área, muerte del miniboss).</summary>
        public void Blast(Vector3 position, float r, Color c)
        {
            mode = Mode.Blast; duration = 0.25f; elapsed = 0f; radius = r; color = c; y = 0.1f;
            transform.position = position;
            Setup(c, 0.15f, false, default, 0f);
            Circle(ring, 0.2f, y);
        }

        /// <summary>Anillo que se desvanece (mejora de torre, UXS-001.3).</summary>
        public void Pulse(Vector3 position, float r, Color c)
        {
            mode = Mode.Pulse; duration = 0.5f; elapsed = 0f; radius = r; color = c; y = 0.09f;
            transform.position = position;
            Setup(c, 0.2f, false, default, 0f);
            Circle(ring, r, y);
        }

        /// <summary>Punto de impacto previsto del Mortero (Doc 04 §18): se cierra mientras cae el proyectil.</summary>
        public void Impact(Vector3 position, float r, float seconds, Color c, Color edgeColor)
        {
            mode = Mode.Impact; duration = Mathf.Max(0.2f, seconds); elapsed = 0f; radius = r; color = c; edge = edgeColor; y = 0.09f;
            transform.position = position;
            Setup(c, 0.12f, true, edgeColor, 0.24f);
            Step(0f);
        }

        void Setup(Color c, float width, bool withEdge, Color edgeColor, float edgeWidth)
        {
            if (ring != null) { ring.widthMultiplier = width; ring.startColor = ring.endColor = c; }
            if (under != null)
            {
                under.gameObject.SetActive(withEdge);
                if (withEdge) { under.widthMultiplier = edgeWidth; under.startColor = under.endColor = edgeColor; }
            }
        }

        public bool Tick(float dt)
        {
            if (mode == Mode.Static) return true;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / duration);
            Step(t);
            return t < 1f;
        }

        void Step(float t)
        {
            switch (mode)
            {
                case Mode.Blast:
                    Circle(ring, Mathf.Lerp(0.2f, radius, t), y);
                    { var c = color; c.a = 1f - t; ring.startColor = ring.endColor = c; }
                    break;
                case Mode.Pulse:
                    { var c = color; c.a = 1f - t; ring.startColor = ring.endColor = c; }
                    break;
                case Mode.Impact:
                    {
                        float r = radius * (1.15f - 0.15f * t);
                        Circle(ring, r, 0.09f);
                        var c = color; c.a = 0.35f + 0.6f * t; ring.startColor = ring.endColor = c;
                        if (under != null) { Circle(under, r, 0.085f); var u = edge; u.a = 0.35f + 0.5f * t; under.startColor = under.endColor = u; }
                        break;
                    }
            }
        }

        public static void Circle(LineRenderer lr, float r, float height)
        {
            if (lr == null) return;
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, height, Mathf.Sin(a) * r));
            }
        }
    }
}
