using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Feedback de ataques de torre:
///  - BeamTick: LineRenderer por torre (0.06 → 0.16 de ancho, naranja → blanco según la rampa), sigue al
///    objetivo entre ticks y se apaga si pasan 0.35 s sin tick.
///  - ChainJump: rayo dentado celeste (6 puntos, jitter 0.15) visible 0.12 s, pool de 8.
///  - PushHit: anillo translúcido que se expande en el enemigo (0.25 s).
///  - TowerFired: sonido (Shoot / ShootHeavy) y retroceso: pulso de escala del hijo "Emblem".
/// </summary>
public class BeamVisuals : MonoBehaviour
{
    public const float BeamTimeout = 0.35f;
    public const float BeamMinWidth = 0.06f;
    public const float BeamMaxWidth = 0.16f;
    public const int ChainPoolSize = 8;
    public const float ChainVisible = 0.12f;
    public const float ChainJitter = 0.15f;
    public const float RecoilSeconds = 0.14f;
    public const float RecoilAmount = 0.45f;

    static readonly Color BeamCold = new Color(1f, 0.55f, 0.15f, 0.9f);
    static readonly Color BeamHot = new Color(1f, 0.98f, 0.9f, 1f);
    static readonly Color ChainColor = new Color(0.62f, 0.82f, 1f, 1f);
    static readonly Color PushColor = new Color(0.7f, 0.9f, 1f, 0.55f);

    class Beam
    {
        public Tower tower;
        public LineRenderer lr;
        public EnemyTD target;
        public float lastTick;
        public float ramp;
        public bool visible;
    }

    class Chain
    {
        public LineRenderer lr;
        public float endTime;
        public bool active;
    }

    class Recoil
    {
        public Tower tower;
        public Transform emblem;
        public SimpleSpin spin;
        public Vector3 baseScale;
        public float startTime;
        public float nextSearch;
        public bool active;
    }

    readonly Dictionary<Tower, Beam> beams = new Dictionary<Tower, Beam>();
    readonly List<Beam> beamList = new List<Beam>(16);
    readonly Dictionary<Tower, Recoil> recoils = new Dictionary<Tower, Recoil>();
    readonly List<Recoil> recoilList = new List<Recoil>(32);
    readonly List<Tower> deadTowers = new List<Tower>();
    Chain[] chains;
    int chainCursor;
    readonly Vector3[] chainPoints = new Vector3[6];

    Material lineMaterial;
    FeelRings rings;
    float nextSweep;

    void Awake()
    {
        lineMaterial = GameFeelKit.MakeLineMaterial();
        rings = GetComponent<FeelRings>();
        if (rings == null) rings = gameObject.AddComponent<FeelRings>();

        chains = new Chain[ChainPoolSize];
        for (int i = 0; i < ChainPoolSize; i++)
        {
            var c = new Chain();
            c.lr = MakeLine("ChainBolt", 6, 0.05f, 0.03f);
            c.lr.startColor = ChainColor;
            c.lr.endColor = new Color(ChainColor.r, ChainColor.g, ChainColor.b, 0.7f);
            c.lr.gameObject.SetActive(false);
            chains[i] = c;
        }
    }

    void OnDestroy()
    {
        GameFeelKit.SafeDestroy(lineMaterial);
    }

    void OnEnable()
    {
        CombatEvents.BeamTick += OnBeamTick;
        CombatEvents.ChainJump += OnChainJump;
        CombatEvents.PushHit += OnPushHit;
        CombatEvents.TowerFired += OnTowerFired;
        CombatEvents.TowerSold += OnTowerSold;
    }

    void OnDisable()
    {
        CombatEvents.BeamTick -= OnBeamTick;
        CombatEvents.ChainJump -= OnChainJump;
        CombatEvents.PushHit -= OnPushHit;
        CombatEvents.TowerFired -= OnTowerFired;
        CombatEvents.TowerSold -= OnTowerSold;

        for (int i = 0; i < recoilList.Count; i++) EndRecoil(recoilList[i]);
        recoilList.Clear();
        recoils.Clear();

        for (int i = 0; i < beamList.Count; i++)
            if (beamList[i].lr != null) Destroy(beamList[i].lr.gameObject);
        beamList.Clear();
        beams.Clear();
    }

    LineRenderer MakeLine(string name, int points, float startWidth, float endWidth)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = points;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        lr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        if (lineMaterial != null) lr.sharedMaterial = lineMaterial;
        return lr;
    }

    // ───────────────────────── rayo ─────────────────────────

    static Vector3 BeamOrigin(Tower tower)
    {
        return tower.front != null ? tower.front.position : tower.transform.position + Vector3.up * 1.5f;
    }

    void OnBeamTick(Tower tower, EnemyTD target, float ramp01)
    {
        if (tower == null || target == null) return;

        Beam b;
        if (!beams.TryGetValue(tower, out b) || b.lr == null)
        {
            if (b != null) RemoveBeam(b);
            b = new Beam();
            b.tower = tower;
            b.lr = MakeLine("Beam", 2, BeamMinWidth, BeamMinWidth);
            beams[tower] = b;
            beamList.Add(b);
        }

        b.target = target;
        b.ramp = Mathf.Clamp01(ramp01);
        b.lastTick = Time.time;
        if (!b.visible)
        {
            b.visible = true;
            b.lr.gameObject.SetActive(true);
        }
        RefreshBeam(b);

        ProceduralAudio.Play(Sfx.Beam, 0.3f + 0.2f * b.ramp);
    }

    void RefreshBeam(Beam b)
    {
        if (b.lr == null || b.tower == null) return;
        Vector3 from = BeamOrigin(b.tower);
        Vector3 to = b.target != null ? b.target.transform.position + Vector3.up * 0.5f : from;

        float w = Mathf.Lerp(BeamMinWidth, BeamMaxWidth, b.ramp);
        // leve vibración del grosor para que se sienta vivo
        w *= 1f + 0.08f * Mathf.Sin(Time.time * 40f);
        b.lr.startWidth = w;
        b.lr.endWidth = w * 0.8f;

        Color c = Color.Lerp(BeamCold, BeamHot, b.ramp);
        b.lr.startColor = c;
        b.lr.endColor = c;
        b.lr.SetPosition(0, from);
        b.lr.SetPosition(1, to);
    }

    void RemoveBeam(Beam b)
    {
        if (b == null) return;
        if (b.lr != null) Destroy(b.lr.gameObject);
        if (b.tower != null) beams.Remove(b.tower);
        int idx = beamList.IndexOf(b);
        if (idx >= 0)
        {
            int last = beamList.Count - 1;
            beamList[idx] = beamList[last];
            beamList.RemoveAt(last);
        }
    }

    // ───────────────────────── cadena ─────────────────────────

    void OnChainJump(Vector3 from, Vector3 to)
    {
        if (chains == null) return;
        var c = TakeChain();
        if (c == null || c.lr == null) return;

        Vector3 dir = to - from;
        float len = dir.magnitude;
        Vector3 n = len > 0.001f ? dir / len : Vector3.forward;
        Vector3 side = Vector3.Cross(n, Vector3.up);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
        side.Normalize();
        Vector3 up2 = Vector3.Cross(side, n).normalized;

        chainPoints[0] = from;
        chainPoints[5] = to;
        for (int i = 1; i < 5; i++)
        {
            float u = i / 5f;
            chainPoints[i] = Vector3.Lerp(from, to, u)
                             + side * Random.Range(-ChainJitter, ChainJitter)
                             + up2 * Random.Range(-ChainJitter, ChainJitter);
        }

        c.lr.positionCount = 6;
        c.lr.SetPositions(chainPoints);
        c.endTime = Time.time + ChainVisible;
        c.active = true;
        c.lr.gameObject.SetActive(true);

        ProceduralAudio.Play(Sfx.Chain, 0.5f);
    }

    Chain TakeChain()
    {
        for (int k = 0; k < ChainPoolSize; k++)
        {
            int i = (chainCursor + k) % ChainPoolSize;
            if (!chains[i].active)
            {
                chainCursor = (i + 1) % ChainPoolSize;
                return chains[i];
            }
        }
        Chain oldest = null;
        float best = float.MaxValue;
        for (int i = 0; i < ChainPoolSize; i++)
        {
            if (chains[i].endTime < best) { best = chains[i].endTime; oldest = chains[i]; }
        }
        return oldest;
    }

    // ───────────────────────── empuje ─────────────────────────

    void OnPushHit(Tower tower, EnemyTD enemy)
    {
        if (enemy == null) return;
        Vector3 pos = enemy.transform.position;
        if (rings != null)
            rings.Spawn(new Vector3(pos.x, 0.06f, pos.z), PushColor, 0.3f, 1.4f, 0.25f);
        ProceduralAudio.Play(Sfx.Push, 0.5f);
    }

    // ───────────────────────── disparo / retroceso ─────────────────────────

    void OnTowerFired(Tower tower, EnemyTD target)
    {
        if (tower == null) return;

        bool heavy = tower.data != null &&
                     (tower.data.attackType == AttackType.Splash || tower.data.attackType == AttackType.Burst);
        ProceduralAudio.Play(heavy ? Sfx.ShootHeavy : Sfx.Shoot, heavy ? 0.55f : 0.45f);

        Recoil r;
        if (!recoils.TryGetValue(tower, out r))
        {
            r = new Recoil();
            r.tower = tower;
            recoils[tower] = r;
            recoilList.Add(r);
            FindEmblem(r);
        }
        else if (r.emblem == null && Time.time >= r.nextSearch)
        {
            FindEmblem(r);   // la torre disparó antes de tener emblema: reintentar cada tanto
        }

        if (r.emblem == null) return;
        if (!r.active)
        {
            r.active = true;
            // Si no hay SimpleSpin escribiendo la escala cada frame, la restauramos nosotros: guardar la base.
            if (!SpinDrivesScale(r)) r.baseScale = r.emblem.localScale;
        }
        r.startTime = Time.time;
    }

    /// <summary>Busca el hijo "Emblem" (lo crea TowerVisual) y lo cachea; si no está, reintenta en 1 s.</summary>
    static void FindEmblem(Recoil r)
    {
        r.nextSearch = Time.time + 1f;
        if (r.tower == null) return;
        r.emblem = r.tower.transform.Find("Emblem");
        if (r.emblem != null)
        {
            r.spin = r.emblem.GetComponent<SimpleSpin>();
            r.baseScale = r.emblem.localScale;
        }
    }

    static bool SpinDrivesScale(Recoil r)
    {
        return r.spin != null && r.spin.isActiveAndEnabled && r.spin.pulse > 0f;
    }

    void EndRecoil(Recoil r)
    {
        if (!r.active) return;
        r.active = false;
        if (r.emblem != null && !SpinDrivesScale(r)) r.emblem.localScale = r.baseScale;
    }

    void OnTowerSold(Tower tower)
    {
        if (tower == null) return;
        Beam b;
        if (beams.TryGetValue(tower, out b)) RemoveBeam(b);
        Recoil r;
        if (recoils.TryGetValue(tower, out r))
        {
            EndRecoil(r);
            RemoveRecoil(r);
        }
    }

    void RemoveRecoil(Recoil r)
    {
        if (r.tower != null) recoils.Remove(r.tower);
        int idx = recoilList.IndexOf(r);
        if (idx >= 0)
        {
            int last = recoilList.Count - 1;
            recoilList[idx] = recoilList[last];
            recoilList.RemoveAt(last);
        }
    }

    // ───────────────────────── update ─────────────────────────

    void Update()
    {
        float now = Time.time;

        for (int i = beamList.Count - 1; i >= 0; i--)
        {
            var b = beamList[i];
            if (b.tower == null || b.lr == null)
            {
                if (b.lr != null) Destroy(b.lr.gameObject);
                int last = beamList.Count - 1;
                beamList[i] = beamList[last];
                beamList.RemoveAt(last);
                continue;
            }
            if (!b.visible) continue;

            if (now - b.lastTick > BeamTimeout || b.target == null)
            {
                b.visible = false;
                b.target = null;
                b.lr.gameObject.SetActive(false);
            }
            else
            {
                RefreshBeam(b);
            }
        }

        if (chains != null)
        {
            for (int i = 0; i < ChainPoolSize; i++)
            {
                var c = chains[i];
                if (!c.active) continue;
                if (c.lr == null) { c.active = false; continue; }
                if (now >= c.endTime)
                {
                    c.active = false;
                    c.lr.gameObject.SetActive(false);
                }
            }
        }

        // Barrido ocasional de torres destruidas por fuera de TowerSold.
        if (now >= nextSweep)
        {
            nextSweep = now + 2f;
            SweepDeadTowers();
        }
    }

    void LateUpdate()
    {
        // Después de SimpleSpin.Update (que reescribe la escala del emblema cada frame).
        float now = Time.time;
        for (int i = recoilList.Count - 1; i >= 0; i--)
        {
            var r = recoilList[i];
            if (!r.active) continue;
            if (r.emblem == null || r.tower == null)
            {
                r.active = false;
                continue;
            }

            float t = (now - r.startTime) / RecoilSeconds;
            if (t >= 1f)
            {
                EndRecoil(r);
                continue;
            }

            float factor = 1f + RecoilAmount * Mathf.Sin(t * Mathf.PI);
            if (SpinDrivesScale(r))
                r.emblem.localScale = r.emblem.localScale * factor;
            else
                r.emblem.localScale = r.baseScale * factor;
        }
    }

    void SweepDeadTowers()
    {
        deadTowers.Clear();
        foreach (var kv in beams) if (kv.Key == null) deadTowers.Add(kv.Key);
        for (int i = 0; i < deadTowers.Count; i++) beams.Remove(deadTowers[i]);

        deadTowers.Clear();
        foreach (var kv in recoils) if (kv.Key == null) deadTowers.Add(kv.Key);
        for (int i = 0; i < deadTowers.Count; i++) recoils.Remove(deadTowers[i]);
        deadTowers.Clear();

        for (int i = recoilList.Count - 1; i >= 0; i--)
        {
            if (recoilList[i].tower == null)
            {
                int last = recoilList.Count - 1;
                recoilList[i] = recoilList[last];
                recoilList.RemoveAt(last);
            }
        }
    }
}
