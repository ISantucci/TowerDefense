using System;
using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Raíz de la escena de un nivel (TL-003: una escena por nivel). Junta las piezas que se acomodan con el mouse
    /// —recorridos y sus puntos, base, área construible, rocas, pista del tutorial— y las convierte en la forma que usa el
    /// núcleo. Al guardar la escena, la forma se hornea en el LevelDefinition (editor); al dar Play desde esta escena, el
    /// juego arranca directo en este nivel.
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("Clash Defense/Nivel/Nivel")]
    public sealed class LevelAuthoring : MonoBehaviour
    {
        [Tooltip("Datos del nivel: id, nombre, oleadas y tema. La forma de esta escena se hornea ahí al guardarla.")]
        public LevelDefinition definition;
        [Min(0.5f), Tooltip("Ancho del camino, en unidades de mundo.")]
        public float pathWidth = 2.2f;

        /// <summary>Redondeo a centésimas: las coordenadas que se hornean no arrastran ruido de punto flotante.</summary>
        public static float Round(float v) => (float)Math.Round(v * 100f) / 100f;
        public static Vec2 Flat(Vector3 p) => new Vec2(Round(p.x), Round(p.z));
        public static Vector3 ToWorld(Vec2 v, float y = 0f) => new Vector3(v.x, y, v.z);

        /// <summary>Recorridos en orden de carril: el orden de la jerarquía.</summary>
        public List<RouteAuthoring> Routes()
        {
            var list = new List<RouteAuthoring>();
            GetComponentsInChildren(true, list);
            return list;
        }

        public BaseAuthoring Base => GetComponentInChildren<BaseAuthoring>(true);
        public BuildArea Area => GetComponentInChildren<BuildArea>(true);
        public TutorialSpot Tutorial => GetComponentInChildren<TutorialSpot>(true);

        public List<RockArea> Rocks()
        {
            var list = new List<RockArea>();
            GetComponentsInChildren(true, list);
            return list;
        }

        /// <summary>La forma del nivel tal como está ahora en la escena.</summary>
        public LevelShape BuildShape()
        {
            var shape = new LevelShape { pathWidth = Round(pathWidth) };
            var b = Base;
            var basePos = b != null ? Flat(b.transform.position) : new Vec2(0f, 0f);
            shape.baseRadius = b != null ? Round(b.radius) : 1.6f;
            var routes = new List<RouteData>();
            foreach (var r in Routes()) routes.Add(new RouteData { points = r.ToPoints(basePos) });
            shape.routes = routes.ToArray();
            var area = Area;
            shape.buildArea = area != null ? area.ToRect() : new RectData();
            var rocks = new List<CircleData>();
            foreach (var r in Rocks()) rocks.Add(r.ToCircle());
            shape.blocked = rocks.ToArray();
            var t = Tutorial;
            shape.tutorialHint = t != null ? t.ToCircle() : new CircleData();
            return shape;
        }

        /// <summary>Problemas de la forma, en palabras (se muestran en el Inspector y frenan el horneado).</summary>
        public List<string> ValidateShape()
        {
            var e = new List<string>();
            if (definition == null) e.Add("falta el LevelDefinition (los datos del nivel)");
            if (Base == null) e.Add("falta la base");
            if (Area == null) e.Add("falta el área construible");
            var routes = Routes();
            if (routes.Count == 0) e.Add("no hay recorridos: agregá uno");
            for (int i = 0; i < routes.Count; i++)
            {
                int n = 0;
                foreach (var p in routes[i].points) if (p != null) n++;
                if (n == 0) e.Add($"el recorrido {i} ({routes[i].name}) no tiene puntos: necesita al menos la entrada");
                if (n != routes[i].points.Count) e.Add($"el recorrido {i} ({routes[i].name}) tiene lugares vacíos en la lista de puntos");
            }
            var area = Area;
            if (area != null && (area.size.x <= 0f || area.size.y <= 0f)) e.Add("el área construible tiene tamaño cero");
            return e;
        }

        void Start()
        {
            // Play desde la escena del nivel: sin juego cargado, se abre el juego y arranca directo acá (TL-003).
            if (Application.isPlaying && definition != null && FindAnyObjectByType<GameBootstrap>() == null)
                GameBootstrap.RequestLevelTest(definition, gameObject.scene);
        }
    }
}
