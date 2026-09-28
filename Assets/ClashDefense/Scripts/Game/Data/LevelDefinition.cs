using System;
using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Forma horneada de un nivel: la escribe su escena al guardarse (LevelBaker). No se edita a mano: para mover un camino,
    /// una roca o la base se abre la escena del nivel.
    /// </summary>
    [Serializable]
    public class LevelShape
    {
        public RouteData[] routes = new RouteData[0];
        public float pathWidth = 2.2f;
        public RectData buildArea = new RectData();
        public CircleData[] blocked = new CircleData[0];
        public float baseRadius = 1.6f;
        public CircleData tutorialHint = new CircleData();

        public bool IsEmpty => routes == null || routes.Length == 0;
    }

    /// <summary>
    /// Un nivel (LDS-002.6): lo que el núcleo necesita para jugarlo. Tiene dos partes:
    ///  · lo que se edita acá, en el Inspector: nombre, oleadas, tema, torre del tutorial;
    ///  · la forma (recorridos, área, rocas, base), que vive en la escena del nivel y se hornea acá al guardarla.
    /// En la partida se usa una copia: nada de lo que pase en el juego cambia el asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Clash Defense/Datos/Nivel", fileName = "Nivel_", order = 20)]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Tooltip("Identificador del nivel (\"m1_n1\"). Lo usan la campaña, el guardado y el registro: no se cambia una vez publicado.")]
        public string id;
        [Tooltip("Nombre largo que se ve en el HUD del Laboratorio y en los registros.")]
        public string displayName;
        public LevelTheme theme;
        [Tooltip("Balance cuyas torres y enemigos usan estas oleadas.")]
        public BalanceDefinition balance;
        [Tooltip("Torre que el tutorial obliga a construir. Vacío = el nivel no tiene tutorial.")]
        public TowerDefinition tutorialTower;
        [Tooltip("Oleadas del nivel, en orden. Vacío = las del balance (solo el Laboratorio).\n" +
                 "sequence: letras de los enemigos separadas por espacio; '.' es un lugar vacío; (D E)x4 repite; '/' separa ráfagas.\n" +
                 "spawnInterval: segundos entre lugares. lanes: a qué recorrido va cada enemigo, en ciclo (\"0 x3 1 x3\").")]
        public List<WaveData> waves = new List<WaveData>();

        [SerializeField, Tooltip("Escena donde se edita la forma del nivel.")]
        string scenePath;
        [SerializeField, Tooltip("Forma horneada desde la escena. No se edita a mano.")]
        LevelShape shape = new LevelShape();

        public string ScenePath => scenePath;
        public LevelShape Shape => shape;

        /// <summary>La escribe el horneado de la escena (editor).</summary>
        internal void SetShape(LevelShape s) { shape = s ?? new LevelShape(); }
        internal void SetScenePath(string path) { scenePath = path; }

        /// <summary>Copia para el núcleo.</summary>
        public LevelData ToCore()
        {
            var s = shape ?? new LevelShape();
            var l = new LevelData
            {
                id = id,
                displayName = displayName,
                routes = CloneRoutes(s.routes),
                pathWidth = s.pathWidth,
                buildArea = DataCopy.Clone(s.buildArea),
                blocked = CloneCircles(s.blocked),
                baseRadius = s.baseRadius,
                tutorialTowerId = tutorialTower != null ? tutorialTower.Id : "",
                tutorialHint = DataCopy.Clone(s.tutorialHint) ?? new CircleData(),
                theme = theme != null ? theme.id : "",
            };
            if (waves != null && waves.Count > 0)
            {
                var w = new WaveData[waves.Count];
                for (int i = 0; i < w.Length; i++) w[i] = DataCopy.Clone(waves[i]);
                l.waves = w;
            }
            return l;
        }

        static RouteData[] CloneRoutes(RouteData[] src)
        {
            if (src == null) return new RouteData[0];
            var r = new RouteData[src.Length];
            for (int i = 0; i < src.Length; i++)
                r[i] = new RouteData { points = src[i]?.points != null ? (Vec2[])src[i].points.Clone() : new Vec2[0] };
            return r;
        }

        static CircleData[] CloneCircles(CircleData[] src)
        {
            if (src == null) return new CircleData[0];
            var r = new CircleData[src.Length];
            for (int i = 0; i < src.Length; i++) r[i] = new CircleData { x = src[i].x, z = src[i].z, radius = src[i].radius };
            return r;
        }

        /// <summary>Lo que el Inspector muestra en rojo. Vacío = el nivel valida contra su balance.</summary>
        public List<string> Validate()
        {
            var e = new List<string>();
            if (string.IsNullOrEmpty(id)) e.Add("falta el id del nivel");
            if (balance == null) e.Add("falta el balance: sin él no se pueden validar las oleadas");
            if (string.IsNullOrEmpty(scenePath)) e.Add("falta la escena del nivel");
            if (shape == null || shape.IsEmpty) { e.Add("la escena todavía no se horneó: abrila y guardala"); return e; }
            e.AddRange(DataValidator.ValidateLevel(balance != null ? balance.ToCore() : null, ToCore()));
            return e;
        }

        /// <summary>Resumen de una oleada para el Inspector: cuántos enemigos y cuánto tarda en salir el último.</summary>
        public static string WaveSummary(WaveData w)
        {
            try
            {
                var slots = WaveSequence.Expand(w.sequence);
                int enemies = 0;
                foreach (var s in slots) if (s != ".") enemies++;
                return $"{enemies} enemigos · {Mathf.Max(0, slots.Count - 1) * w.spawnInterval:0.#} s de aparición";
            }
            catch (FormatException ex) { return "error: " + ex.Message; }
        }
    }
}
