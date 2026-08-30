using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Destello al recibir daño: durante 0.08 s los renderers del enemigo se ven blanqueados.
/// Se implementa con MaterialPropertyBlock (override de _Color por renderer): el material instanciado
/// por EnemyVisual nunca se toca, así que al terminar el flash se restaura el color exacto previo
/// (incluso si EnemyVisual.SetLeader lo cambió durante el flash) y los flashes superpuestos sólo
/// extienden el tiempo, sin corromper el color base.
/// </summary>
public class HitFlash : MonoBehaviour
{
    public const float FlashSeconds = 0.08f;
    public const float Whiten = 0.7f;

    class State
    {
        public EnemyTD enemy;
        public Renderer[] renderers;
        public float endTime;
        public bool active;
    }

    readonly Dictionary<EnemyTD, State> states = new Dictionary<EnemyTD, State>();
    readonly List<State> list = new List<State>(64);
    readonly List<Renderer> tmpRenderers = new List<Renderer>(8);
    readonly List<Renderer> tmpFiltered = new List<Renderer>(8);
    readonly List<EnemyTD> deadKeys = new List<EnemyTD>();

    MaterialPropertyBlock block;
    MaterialPropertyBlock emptyBlock;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        block = new MaterialPropertyBlock();
        emptyBlock = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        CombatEvents.DamageDealt += OnDamageDealt;
        CombatEvents.EnemyDied += OnEnemyGone;
        CombatEvents.EnemyReachedEnd += OnEnemyGone;
    }

    void OnDisable()
    {
        CombatEvents.DamageDealt -= OnDamageDealt;
        CombatEvents.EnemyDied -= OnEnemyGone;
        CombatEvents.EnemyReachedEnd -= OnEnemyGone;

        for (int i = 0; i < list.Count; i++)
            if (list[i].active) ClearFlash(list[i]);
        list.Clear();
        states.Clear();
    }

    void OnDamageDealt(EnemyTD enemy, int amount, Vector3 pos)
    {
        ProceduralAudio.Play(Sfx.Hit, 0.4f);
        if (enemy == null) return;

        State s;
        if (!states.TryGetValue(enemy, out s))
        {
            s = new State();
            s.enemy = enemy;
            s.renderers = CollectRenderers(enemy);
            states[enemy] = s;
            list.Add(s);
        }

        ApplyFlash(s);
        s.endTime = Time.time + FlashSeconds;
        s.active = true;
    }

    void OnEnemyGone(EnemyTD enemy)
    {
        if (enemy == null) return;
        State s;
        if (!states.TryGetValue(enemy, out s)) return;
        if (s.active) ClearFlash(s);
        RemoveState(s);
    }

    Renderer[] CollectRenderers(EnemyTD enemy)
    {
        tmpRenderers.Clear();
        tmpFiltered.Clear();
        enemy.GetComponentsInChildren<Renderer>(true, tmpRenderers);
        for (int i = 0; i < tmpRenderers.Count; i++)
        {
            var r = tmpRenderers[i];
            if (r == null || r is LineRenderer || r is TrailRenderer) continue;
            string n = r.name;
            if (n == "Shadow" || n.StartsWith(EnemyHealthBars.HolderName)) continue;
            var m = r.sharedMaterial;
            if (m == null || !m.HasProperty("_Color")) continue;
            tmpFiltered.Add(r);
        }
        var arr = tmpFiltered.ToArray();
        tmpRenderers.Clear();
        tmpFiltered.Clear();
        return arr;
    }

    void ApplyFlash(State s)
    {
        var rs = s.renderers;
        if (rs == null) return;
        for (int i = 0; i < rs.Length; i++)
        {
            var r = rs[i];
            if (r == null) continue;
            var m = r.sharedMaterial;
            if (m == null) continue;
            // Color base actual (puede incluir el tinte de líder): sólo lo leemos, nunca lo escribimos.
            Color baseColor = m.color;
            Color flash = Color.Lerp(baseColor, Color.white, Whiten);
            flash.a = baseColor.a;
            block.SetColor(ColorId, flash);
            r.SetPropertyBlock(block);
        }
    }

    void ClearFlash(State s)
    {
        s.active = false;
        var rs = s.renderers;
        if (rs == null) return;
        for (int i = 0; i < rs.Length; i++)
        {
            var r = rs[i];
            if (r == null) continue;
            r.SetPropertyBlock(emptyBlock);   // bloque vacío = sin overrides → vuelve el color del material
        }
    }

    void RemoveState(State s)
    {
        if (s.enemy != null) states.Remove(s.enemy);
        int idx = list.IndexOf(s);
        if (idx >= 0)
        {
            int last = list.Count - 1;
            list[idx] = list[last];
            list.RemoveAt(last);
        }
    }

    void Update()
    {
        float now = Time.time;
        bool sawDead = false;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            var s = list[i];
            if (s.enemy == null)
            {
                sawDead = true;
                int last = list.Count - 1;
                list[i] = list[last];
                list.RemoveAt(last);
                continue;
            }
            if (s.active && now >= s.endTime) ClearFlash(s);
        }

        if (sawDead) PruneDeadKeys();
    }

    void PruneDeadKeys()
    {
        deadKeys.Clear();
        foreach (var kv in states)
            if (kv.Key == null) deadKeys.Add(kv.Key);
        for (int i = 0; i < deadKeys.Count; i++) states.Remove(deadKeys[i]);
        deadKeys.Clear();
    }
}
