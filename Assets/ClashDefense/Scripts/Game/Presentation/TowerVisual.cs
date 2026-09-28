using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Cómo se ve una torre (UXS-001.2, UXS-002.4): va en el prefab de la torre. El nivel 2 estira el cuerpo y muestra la banda
    /// clara. Las partes animadas (caño que apunta, orbe, monedas de la torre de oro) son referencias opcionales del prefab:
    /// un modelo de arte nuevo las vuelve a asignar en el Inspector y el código no cambia.
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("Clash Defense/Presentación/Torre")]
    public sealed class TowerVisual : MonoBehaviour
    {
        [Tooltip("Lo que se estira al pasar a nivel 2.")]
        [SerializeField] internal Transform body;
        [Tooltip("Lo que gira hacia el objetivo al disparar (caño, tubo, boquilla). Opcional.")]
        [SerializeField] internal Transform aim;
        [Tooltip("Orbe o cristal animado. Opcional.")]
        [SerializeField] internal Transform orb;
        [SerializeField] internal bool orbSpins;
        [SerializeField] internal float orbBaseY = 1.9f;
        [Tooltip("Pila de monedas de la torre de oro: crece con lo guardado. Opcional.")]
        [SerializeField] internal Transform coins;
        [Tooltip("Marca de \"llena\" de la torre de oro. Opcional.")]
        [SerializeField] internal Transform fullMark;
        [Tooltip("Banda del nivel 2. Opcional.")]
        [SerializeField] internal GameObject bandN2;
        [SerializeField] internal float n2Stretch = 1.2f;

        Renderer[] renderers;
        MaterialPropertyBlock mpb;

        public Tower Tower { get; private set; }

        public void Bind(Tower t)
        {
            Tower = t;
            transform.localScale = Vector3.one;
            SetLevel(t != null ? t.Level : 1);
        }

        public void SetLevel(int level)
        {
            if (body != null) body.localScale = new Vector3(1f, level >= 2 ? n2Stretch : 1f, 1f);
            if (bandN2 != null) bandN2.SetActive(level >= 2);
        }

        public void Aim(Vector3 target)
        {
            if (aim == null) return;
            var d = target - aim.position; d.y = 0;
            if (d.sqrMagnitude > 1e-4f) aim.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        }

        public void Animate(float time)
        {
            int seed = Tower != null ? Tower.Id : 0;
            if (orb != null)
            {
                if (orbSpins) orb.localRotation = Quaternion.Euler(0f, 45f + time * 40f, 0f);
                else orb.localPosition = new Vector3(0, orbBaseY + 0.12f * Mathf.Sin(time * 2.5f + seed), 0);
            }
            if (coins != null && Tower != null)
            {
                float k = Tower.Stats.goldCapacity > 0f ? Mathf.Clamp01(Tower.Stored / Tower.Stats.goldCapacity) : 0f;
                coins.localScale = new Vector3(0.6f + 0.5f * k, 0.3f + 2.2f * k, 0.6f + 0.5f * k);
                if (fullMark != null)
                {
                    bool full = Tower.Full;
                    if (fullMark.gameObject.activeSelf != full) fullMark.gameObject.SetActive(full);
                    if (full) fullMark.localPosition = new Vector3(0, 2.1f + 0.18f * Mathf.Abs(Mathf.Sin(time * 5f)), 0);
                }
            }
        }

        /// <summary>Fantasma de colocación: el mismo modelo, transparente (UXS-001.2). Se hace una vez por tipo y se reusa.</summary>
        public void MakeGhost(Material ghostMaterial, float alpha)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            mpb = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                var c = r.sharedMaterial != null ? r.sharedMaterial.color : Color.white;
                if (ghostMaterial != null) r.sharedMaterial = ghostMaterial;
                c.a = alpha;
                mpb.SetColor(ColorId, c);
                r.SetPropertyBlock(mpb);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Tower = null;
            if (coins != null) coins.localScale = new Vector3(0.6f, 0.3f, 0.6f);
            if (fullMark != null) fullMark.gameObject.SetActive(false);
        }

        internal static readonly int ColorId = Shader.PropertyToID("_Color");
    }
}
