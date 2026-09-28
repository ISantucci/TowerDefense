using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Cómo se ve un enemigo (UXS-001.4, UXS-002.5): va en el prefab del enemigo. Barras de vida y de metal que miran a la
    /// cámara, destello al recibir daño, llama mientras se quema y la armadura que se cae. Los prefabs se reciclan: Bind deja
    /// todo como nuevo, así que un enemigo no se crea ni se destruye durante la partida.
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("Clash Defense/Presentación/Enemigo")]
    public sealed class EnemyVisual : MonoBehaviour
    {
        [Tooltip("Lo que gira hacia donde camina.")]
        [SerializeField] internal Transform body;
        [Tooltip("Raíz de las barras (mira a la cámara).")]
        [SerializeField] internal Transform barRoot;
        [Tooltip("Pivote del relleno de la barra de vida (se escala en X).")]
        [SerializeField] internal Transform hpFill;
        [Tooltip("Pivote del relleno de la barra de metal. Opcional.")]
        [SerializeField] internal Transform armorFill;
        [SerializeField] internal GameObject armorBar;
        [Tooltip("Armadura que se cae al romperse. Opcional.")]
        [SerializeField] internal GameObject shell;
        [Tooltip("Llama visible mientras se quema. Opcional.")]
        [SerializeField] internal GameObject flame;
        [Tooltip("Partes que destellan en blanco al recibir daño.")]
        [SerializeField] internal Renderer[] tint = new Renderer[0];
        [Tooltip("Altura a la que apuntan los rayos (aéreos más alto).")]
        [SerializeField] internal float aimHeight = 0.8f;

        MaterialPropertyBlock mpb;
        float flash;
        bool tinted;
        // muerte
        bool dying, arrived;
        float dieElapsed, dieDuration;
        Vector3 dieFrom, dieTo;

        public Enemy Enemy { get; private set; }
        public float AimHeight => aimHeight;
        public bool Dying => dying;

        void Awake() { mpb = new MaterialPropertyBlock(); }

        public void Bind(Enemy e)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            Enemy = e;
            dying = false; arrived = false; flash = 0f;
            transform.localScale = Vector3.one;
            bool armored = e != null && e.Type.armor > 0f;
            if (shell != null) shell.SetActive(armored);
            if (armorBar != null) armorBar.SetActive(armored);
            if (flame != null) flame.SetActive(false);
            if (hpFill != null) hpFill.localScale = Vector3.one;
            if (armorFill != null) armorFill.localScale = Vector3.one;
            ClearTint();
        }

        public void Tick(Vector3 position, Vector3 direction, Quaternion cameraRotation, float time)
        {
            if (Enemy == null || dying) return;
            transform.position = position;
            if (body != null && direction.sqrMagnitude > 1e-6f) body.rotation = Quaternion.LookRotation(direction, Vector3.up);
            if (barRoot != null) barRoot.rotation = cameraRotation;
            var t = Enemy.Type;
            if (hpFill != null) hpFill.localScale = new Vector3(Mathf.Clamp01(Enemy.Hp / Mathf.Max(1f, t.hp)), 1f, 1f);
            if (armorFill != null && t.armor > 0f) armorFill.localScale = new Vector3(Mathf.Clamp01(Enemy.Armor / t.armor), 1f, 1f);
            if (flame != null)
            {
                bool burning = Enemy.Burning;
                if (flame.activeSelf != burning) flame.SetActive(burning);
                if (burning) flame.transform.localScale = new Vector3(0.35f, 0.5f + 0.15f * Mathf.Sin(time * 18f + Enemy.Id), 0.35f);
            }
            if (flash > 0f)
            {
                flash -= Time.deltaTime;
                if (flash > 0f) SetTint(Color.white); else ClearTint();
            }
        }

        /// <summary>Destello blanco al recibir daño (UXS-001.4). Un golpe de quemadura destella menos.</summary>
        public void Flash(float seconds) { flash = Mathf.Max(flash, seconds); }

        /// <summary>La armadura se rompe: se esconde acá y el efecto de la caída lo hace un pedazo reciclado.</summary>
        public Transform BreakShell()
        {
            if (armorBar != null) armorBar.SetActive(false);
            if (shell == null || !shell.activeSelf) return null;
            shell.SetActive(false);
            return shell.transform;
        }

        public void BeginDeath(bool reachedBase, Vector3 basePosition, bool miniboss)
        {
            dying = true;
            arrived = reachedBase;
            dieElapsed = 0f;
            dieDuration = reachedBase ? 0.35f : miniboss ? 0.6f : 0.2f;
            dieFrom = transform.position;
            dieTo = basePosition;
            if (flame != null) flame.SetActive(false);
            ClearTint();
        }

        /// <summary>Avanza la muerte. Devuelve false cuando terminó (el prefab vuelve a la reserva).</summary>
        public bool TickDeath(float dt)
        {
            dieElapsed += dt;
            float t = Mathf.Clamp01(dieElapsed / dieDuration);
            if (arrived) transform.position = Vector3.Lerp(dieFrom, dieTo, t);
            transform.localScale = Vector3.one * (1f - t);
            return t < 1f;
        }

        void SetTint(Color c)
        {
            mpb.SetColor(TowerVisual.ColorId, c);
            foreach (var r in tint) if (r != null) r.SetPropertyBlock(mpb);
            tinted = true;
        }

        void ClearTint()
        {
            if (!tinted) return;
            foreach (var r in tint) if (r != null) r.SetPropertyBlock(null);
            tinted = false;
        }
    }
}
