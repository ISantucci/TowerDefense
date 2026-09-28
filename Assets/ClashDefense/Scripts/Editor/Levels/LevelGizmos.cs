using System.Collections.Generic;
using ClashDefense.Game;
using UnityEditor;
using UnityEngine;

namespace ClashDefense.EditorTools
{
    /// <summary>
    /// Dibujo de las piezas del nivel en la vista de escena (solo editor): recorridos con su carril y su sentido, entradas,
    /// base, área construible, rocas y pista del tutorial. Los puntos se pueden seleccionar haciendo clic en su esfera.
    /// Vive en el editor (DrawGizmo) para que los scripts del juego no carguen código de dibujo.
    /// </summary>
    static class LevelGizmos
    {
        static readonly Color PointColor = new Color(1f, 0.85f, 0.25f, 0.95f);
        static readonly Color AreaColor = new Color(0.45f, 0.95f, 0.55f, 0.9f);
        static readonly Color RockColor = new Color(1f, 0.45f, 0.4f, 0.9f);
        static readonly Color BaseColor = new Color(0.4f, 0.8f, 1f, 1f);
        static readonly Color TutorialColor = new Color(1f, 0.8f, 0.2f, 0.9f);
        const float Y = 0.3f;

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        static void DrawPoint(Waypoint w, GizmoType type)
        {
            bool sel = (type & GizmoType.Selected) != 0;
            Gizmos.color = sel ? Color.white : PointColor;
            Gizmos.DrawSphere(w.transform.position + Vector3.up * Y, sel ? 0.45f : 0.35f);
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        static void DrawRock(RockArea r, GizmoType type)
        {
            Handles.color = RockColor;
            Handles.DrawWireDisc(r.transform.position + Vector3.up * 0.05f, Vector3.up, r.radius, 2f);
            Gizmos.color = new Color(RockColor.r, RockColor.g, RockColor.b, 0.25f);
            Gizmos.DrawSphere(r.transform.position + Vector3.up * 0.2f, 0.25f);
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        static void DrawBase(BaseAuthoring b, GizmoType type)
        {
            Handles.color = BaseColor;
            Handles.DrawWireDisc(b.transform.position + Vector3.up * 0.05f, Vector3.up, b.radius, 3f);
            Gizmos.color = new Color(BaseColor.r, BaseColor.g, BaseColor.b, 0.3f);
            Gizmos.DrawCube(b.transform.position + Vector3.up * 0.4f, new Vector3(0.6f, 0.6f, 0.6f));
            Handles.Label(b.transform.position + Vector3.up * 3.2f, "BASE", Label(BaseColor));
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        static void DrawTutorial(TutorialSpot t, GizmoType type)
        {
            Handles.color = TutorialColor;
            Handles.DrawWireDisc(t.transform.position + Vector3.up * 0.08f, Vector3.up, t.radius, 2f);
            Handles.Label(t.transform.position + Vector3.up * 1.2f, "pista del tutorial", Label(TutorialColor));
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawArea(BuildArea a, GizmoType type)
        {
            var c = a.transform.position; c.y = 0.04f;
            float hx = a.size.x * 0.5f, hz = a.size.y * 0.5f;
            var pts = new[] { c + new Vector3(-hx, 0, -hz), c + new Vector3(-hx, 0, hz), c + new Vector3(hx, 0, hz), c + new Vector3(hx, 0, -hz) };
            Handles.DrawSolidRectangleWithOutline(pts, new Color(AreaColor.r, AreaColor.g, AreaColor.b, (type & GizmoType.Selected) != 0 ? 0.06f : 0.02f), AreaColor);
            Handles.Label(c + new Vector3(0f, 0f, -hz - 0.9f), "área construible", Label(AreaColor));
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawLevel(LevelAuthoring level, GizmoType type)
        {
            var b = level.Base;
            if (b == null) return;
            var basePos = b.transform.position;
            var routes = level.Routes();
            // una etiqueta por entrada, con todos los carriles que salen de ahí (en el nivel 6 salen dos por entrada)
            var starts = new List<Vector3>();
            var lanes = new List<string>();
            var colors = new List<Color>();
            for (int i = 0; i < routes.Count; i++)
            {
                var r = routes[i];
                var pts = new List<Vector3>();
                foreach (var p in r.points) if (p != null) pts.Add(Flat(p.transform.position));
                pts.Add(Flat(basePos));
                if (pts.Count < 2) continue;
                bool selected = Selection.Contains(r.gameObject);
                Handles.color = selected ? Color.white : r.gizmoColor;
                Handles.DrawAAPolyLine(selected ? 7f : 4f, pts.ToArray());
                // sentido: una flecha a mitad de cada tramo
                for (int k = 1; k < pts.Count; k++)
                {
                    var dir = pts[k] - pts[k - 1];
                    if (dir.sqrMagnitude < 0.01f) continue;
                    var mid = (pts[k] + pts[k - 1]) * 0.5f;
                    Handles.ConeHandleCap(0, mid, Quaternion.LookRotation(dir.normalized, Vector3.up), 0.6f, EventType.Repaint);
                }
                var start = pts[0];
                int at = starts.FindIndex(x => (x - start).sqrMagnitude < 0.01f);
                if (at < 0) { starts.Add(start); lanes.Add(i.ToString()); colors.Add(r.gizmoColor); }
                else lanes[at] += ", " + i;
            }
            for (int k = 0; k < starts.Count; k++)
            {
                bool many = lanes[k].Contains(",");
                Handles.Label(starts[k] + Vector3.up * 2.2f, $"ENTRADA\n{(many ? "carriles" : "carril")} {lanes[k]}", Label(colors[k]));
            }
        }

        static Vector3 Flat(Vector3 p) => new Vector3(p.x, Y, p.z);

        static GUIStyle Label(Color c)
        {
            var s = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
            s.normal.textColor = c;
            return s;
        }
    }
}
