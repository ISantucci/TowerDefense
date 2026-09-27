using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Sonidos sintéticos generados al arrancar (SOL-001 D8): el piso de feedback pide visual + sonido,
    /// y el Doc 05 §2.2 deja el audio definitivo afuera. Un sonido distinto por evento, con la jerarquía del libro 04:
    /// filtración > ataque inválido > amenaza nueva > baja y oro > disparo.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        AudioSource src;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        float lastGold;
        readonly Dictionary<int, int> stage = new Dictionary<int, int>();
        public float Volume { get => src != null ? src.volume : 1f; set { if (src != null) src.volume = Mathf.Clamp01(value); } }
        public bool Muted { get => src != null && src.mute; set { if (src != null) src.mute = value; } }

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.ignoreListenerPause = true;
            Volume = PlayerPrefs.GetFloat("cd_volumen", 0.7f);
            Muted = PlayerPrefs.GetInt("cd_silencio", 0) == 1;
            Make("click",     new[] { N(1000, 0.03f, 0.25f, W.Square) });
            Make("rechazo",   new[] { N(150, 0.12f, 0.35f, W.Square) });
            Make("construir", new[] { N(110, 0.12f, 0.8f, W.Sine), N(220, 0.08f, 0.3f, W.Noise) });
            Make("mejorar",   new[] { N(440, 0.07f, 0.45f, W.Sine), N(660, 0.07f, 0.45f, W.Sine), N(880, 0.12f, 0.45f, W.Sine) }, sequential: true);
            Make("vender",    new[] { N(660, 0.07f, 0.4f, W.Sine), N(440, 0.1f, 0.4f, W.Sine) }, sequential: true);
            Make("oro",       new[] { N(1320, 0.05f, 0.22f, W.Sine), N(1760, 0.06f, 0.18f, W.Sine) }, sequential: true);
            Make("inmune",    new[] { N(2900, 0.12f, 0.35f, W.Sine), N(3700, 0.1f, 0.25f, W.Sine) });
            Make("armadura",  new[] { N(300, 0.18f, 0.6f, W.Noise) });
            Make("base",      new[] { N(70, 0.28f, 1.0f, W.Sine), N(90, 0.2f, 0.7f, W.Noise) });
            Make("impacto",   new[] { N(180, 0.05f, 0.18f, W.Noise) });
            Make("cuenta",    new[] { N(600, 0.1f, 0.5f, W.Sine) });
            Make("defense",   new[] { N(880, 0.3f, 0.6f, W.Square) });
            Make("oleada",    new[] { N(220, 0.18f, 0.5f, W.Saw), N(330, 0.28f, 0.5f, W.Saw) }, sequential: true);
            Make("cierre",    new[] { N(523, 0.25f, 0.35f, W.Sine), N(659, 0.25f, 0.35f, W.Sine), N(784, 0.3f, 0.35f, W.Sine) });
            Make("nuevo",     new[] { N(988, 0.1f, 0.4f, W.Square), N(740, 0.14f, 0.4f, W.Square) }, sequential: true);
            Make("victoria",  new[] { N(523, 0.12f, 0.5f, W.Sine), N(659, 0.12f, 0.5f, W.Sine), N(784, 0.12f, 0.5f, W.Sine), N(1046, 0.4f, 0.5f, W.Sine) }, sequential: true);
            Make("derrota",   new[] { N(392, 0.2f, 0.5f, W.Saw), N(330, 0.2f, 0.5f, W.Saw), N(262, 0.5f, 0.5f, W.Saw) }, sequential: true);
            // Mundo 1 (UXS-002.4 y .5): un sonido por mecánica nueva, debajo de filtración e inmunidad en la jerarquía
            Make("mortero",   new[] { N(60, 0.22f, 0.9f, W.Sine), N(120, 0.1f, 0.4f, W.Noise) });
            Make("electrica", new[] { N(1800, 0.06f, 0.25f, W.Noise), N(2400, 0.05f, 0.18f, W.Square) });
            Make("infernal",  new[] { N(180, 0.12f, 0.3f, W.Saw), N(270, 0.12f, 0.25f, W.Saw) });
            Make("llamas",    new[] { N(400, 0.35f, 0.45f, W.Noise) });
            Make("oro_recoger", new[] { N(1568, 0.05f, 0.3f, W.Sine), N(2093, 0.05f, 0.3f, W.Sine), N(2637, 0.08f, 0.3f, W.Sine) }, sequential: true);
            Make("oro_lleno", new[] { N(1318, 0.08f, 0.3f, W.Square), N(1318, 0.08f, 0.3f, W.Square) }, sequential: true);
            Make("miniboss",  new[] { N(110, 0.45f, 0.8f, W.Saw), N(82, 0.55f, 0.8f, W.Saw) }, sequential: true);
            Make("desbloqueo", new[] { N(659, 0.1f, 0.45f, W.Sine), N(880, 0.1f, 0.45f, W.Sine), N(1175, 0.25f, 0.45f, W.Sine) }, sequential: true);
            Make("compra",    new[] { N(988, 0.06f, 0.35f, W.Sine), N(1318, 0.12f, 0.35f, W.Sine) }, sequential: true);
        }

        public void Play(string name, float vol = 1f)
        {
            if (clips.TryGetValue(name, out var c)) src.PlayOneShot(c, vol);
        }

        /// <summary>Qué tipos de enemigo son miniboss (se lee de los datos, no de ids fijos).</summary>
        public System.Func<string, bool> IsMiniboss = id => false;

        /// <summary>Partida nueva: los ids de torre se reusan, las etapas infernales empiezan de cero.</summary>
        public void ResetMatch() => stage.Clear();

        public void OnEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Countdown: Play(e.Text == "DEFENSE" ? "defense" : "cuenta"); break;
                case SimEventType.WaveStarted: Play("oleada"); break;
                case SimEventType.WaveCleared: Play("cierre", 0.8f); break;
                case SimEventType.TowerBuilt: Play("construir"); break;
                case SimEventType.TowerUpgraded: Play("mejorar"); break;
                case SimEventType.TowerSold: Play("vender"); break;
                case SimEventType.ActionRejected: Play("rechazo", 0.7f); break;
                case SimEventType.AttackImmune: if (e.Int2 != 1) Play("inmune", 0.8f); break;   // el calentamiento infernal no suena a inmune
                case SimEventType.ArmorBroken: Play("armadura", 0.9f); break;
                case SimEventType.BaseDamaged: Play("base"); break;
                case SimEventType.ProjectileImpact: Play("impacto", 0.35f); break;
                case SimEventType.EnemyKilled:
                    if (Time.unscaledTime - lastGold > 0.06f) { Play("oro", 0.6f); lastGold = Time.unscaledTime; }
                    break;
                case SimEventType.Shot:
                    switch (e.Text)
                    {
                        case "mortero": Play("mortero", 0.7f); break;
                        case "electrica": if (e.Int2 <= 1) Play("electrica", 0.35f); break;
                        case "lanzallamas": Play("llamas", 0.6f); break;
                        case "infernal":
                            if (!stage.TryGetValue(e.TowerId, out var st) || st != e.Int2) { stage[e.TowerId] = e.Int2; Play("infernal", 0.25f + 0.1f * e.Int2); }   // inicio (etapa 1, también al volver a fijar) y cada subida (Doc 04 §18)
                            break;
                    }
                    break;
                case SimEventType.GoldCollected: Play("oro_recoger", 0.8f); break;
                case SimEventType.GoldStoredFull: Play("oro_lleno", 0.6f); break;
                case SimEventType.EnemySpawned: if (IsMiniboss(e.Text)) Play("miniboss"); break;
                case SimEventType.MatchEnded: Play(e.Text == "victoria" ? "victoria" : e.Text == "derrota" ? "derrota" : "click"); break;
            }
        }

        // ---------------------------------------------------------------- síntesis
        enum W { Sine, Square, Saw, Noise }
        struct Note { public float f, d, a; public W w; }
        static Note N(float f, float d, float a, W w) => new Note { f = f, d = d, a = a, w = w };

        void Make(string name, Note[] notes, bool sequential = false)
        {
            float total = 0f;
            foreach (var n in notes) total = sequential ? total + n.d : Mathf.Max(total, n.d);
            int len = Mathf.Max(1, (int)(total * Rate));
            var data = new float[len];
            var rng = new System.Random(name.GetHashCode());
            float offset = 0f;
            foreach (var n in notes)
            {
                int start = (int)(offset * Rate), count = (int)(n.d * Rate);
                for (int i = 0; i < count && start + i < len; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1f, t / 0.004f) * Mathf.Pow(1f - i / (float)count, 2f);
                    float ph = t * n.f;
                    float s;
                    switch (n.w)
                    {
                        case W.Square: s = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * ph)) * 0.5f; break;
                        case W.Saw: s = (ph - Mathf.Floor(ph)) * 2f - 1f; s *= 0.5f; break;
                        case W.Noise: s = (float)(rng.NextDouble() * 2.0 - 1.0); break;
                        default: s = Mathf.Sin(2f * Mathf.PI * ph); break;
                    }
                    data[start + i] += s * env * n.a * 0.5f;
                }
                if (sequential) offset += n.d;
            }
            var clip = AudioClip.Create("cd_" + name, len, 1, Rate, false);
            clip.SetData(data, 0);
            clips[name] = clip;
        }
    }
}
