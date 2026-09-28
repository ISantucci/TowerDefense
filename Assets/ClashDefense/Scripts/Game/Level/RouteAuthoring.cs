using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Un recorrido: sus puntos en orden, desde la entrada. Después del último punto llega solo a la base.
    /// El orden de los recorridos en la jerarquía es su número de carril (las oleadas reparten por carril: "0 x3 1 x3").
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("Clash Defense/Nivel/Recorrido")]
    public sealed class RouteAuthoring : MonoBehaviour
    {
        [Tooltip("Puntos en orden, desde la entrada. Después del último, el recorrido llega solo a la base.\n" +
                 "Un punto puede estar en varios recorridos: así se comparten tramos (bifurcación o convergencia).")]
        public List<Waypoint> points = new List<Waypoint>();
        [Tooltip("Color de la línea en la vista de escena (solo editor).")]
        public Color gizmoColor = new Color(1f, 0.78f, 0.2f);

        /// <summary>Puntos del recorrido completo: los del recorrido y, al final, la base.</summary>
        public Vec2[] ToPoints(Vec2 basePos)
        {
            var list = new List<Vec2>();
            foreach (var p in points) if (p != null) list.Add(p.Flat);
            list.Add(basePos);
            return list.ToArray();
        }
    }
}
