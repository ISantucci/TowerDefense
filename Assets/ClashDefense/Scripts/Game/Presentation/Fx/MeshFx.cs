using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Efecto de una sola malla que se desvanece: chispa de ataque inválido, llamarada del lanzallamas, fuego en el piso y la
    /// armadura que se cae. El color va por MaterialPropertyBlock: el material es uno solo, compartido, y no se crean
    /// materiales durante la partida (Vaultrum: Memory Leak). Se recicla.
    /// </summary>
    [AddComponentMenu("Clash Defense/Efectos/Efecto de malla")]
    public sealed class MeshFx : MonoBehaviour
    {
        [SerializeField] internal Renderer rend;

        enum Kind { Spark, Flame, Shell, Fire }
        Kind kind;
        MaterialPropertyBlock mpb;
        Color color;
        float duration, elapsed, width, length;
        Vector3 start, spin;

        void Awake() { if (rend == null) rend = GetComponentInChildren<Renderer>(); }

        void SetColor(Color c)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            mpb.SetColor(TowerVisual.ColorId, c);
            rend.SetPropertyBlock(mpb);
        }

        /// <summary>Chispa blanca de un ataque sin daño (UXS-001.4: INMUNE / CALENTANDO).</summary>
        public void Spark(Vector3 position, Color c)
        {
            kind = Kind.Spark; duration = 0.18f; elapsed = 0f; color = c;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            transform.localScale = Vector3.one * 0.2f;
            SetColor(c);
        }

        /// <summary>Ráfaga del lanzallamas (Doc 04 §6.6): un tramo recto que se abre y se apaga.</summary>
        public void Flame(Vector3 from, Vector3 to, float w, Color c)
        {
            kind = Kind.Flame; duration = 0.35f; elapsed = 0f; color = c;
            var dir = to - from;
            width = Mathf.Max(0.3f, w); length = dir.magnitude;
            transform.SetPositionAndRotation((from + to) * 0.5f + Vector3.up * 0.8f, dir.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity);
            transform.localScale = new Vector3(width, 0.4f, length);
            var k = c; k.a = 0.85f; SetColor(k);
        }

        /// <summary>La armadura del Duende blindado se cae girando (UXS-001.4).</summary>
        public void Shell(Transform source, Color c)
        {
            kind = Kind.Shell; duration = 0.5f; elapsed = 0f; color = c;
            transform.SetPositionAndRotation(source.position, source.rotation);
            transform.localScale = source.lossyScale;
            start = source.position;
            spin = new Vector3(Random.Range(-1f, 1f), 0.3f, Random.Range(-1f, 1f)).normalized;
            SetColor(c);
        }

        /// <summary>Fuego en el piso (Doc 04 §6.6): lo mueve WorldView desde el estado del núcleo mientras exista.</summary>
        public void Fire(Vector3 a, Vector3 b, float halfWidth, Color c)
        {
            kind = Kind.Fire; color = c;
            var dir = b - a;
            transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * 0.06f, dir.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity);
            transform.localScale = new Vector3(halfWidth * 2f, 0.04f, dir.magnitude);
        }

        public void FireAlpha(float a) { var c = color; c.a = a; SetColor(c); }

        public bool Tick(float dt)
        {
            if (kind == Kind.Fire) return true;
            elapsed += dt;
            float t = Mathf.Clamp01(elapsed / duration);
            switch (kind)
            {
                case Kind.Spark:
                    transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 0.9f, t);
                    { var c = color; c.a = 1f - t; SetColor(c); }
                    break;
                case Kind.Flame:
                    { var c = color; c.a = 0.85f * (1f - t); SetColor(c); }
                    transform.localScale = new Vector3(width * (1f + 0.4f * t), 0.4f * (1f - t * 0.5f), length);
                    break;
                case Kind.Shell:
                    transform.position = start + new Vector3(0, 1.2f * t - 2.5f * t * t, 0) + spin * t * 0.8f;
                    transform.Rotate(spin, 720f * dt, Space.World);
                    { var c = color; c.a = 1f - t; SetColor(c); }
                    break;
            }
            return t < 1f;
        }
    }
}
