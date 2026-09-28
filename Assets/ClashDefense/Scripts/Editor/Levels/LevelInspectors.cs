using System.Collections.Generic;
using ClashDefense.Game;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ClashDefense.EditorTools
{
    /// <summary>Inspector de la raíz del nivel: cómo se edita, los carriles, la validación en vivo y los botones de trabajo.</summary>
    [CustomEditor(typeof(LevelAuthoring))]
    sealed class LevelAuthoringEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var a = (LevelAuthoring)target;
            EditorGUILayout.HelpBox(
                "Cómo se edita este nivel\n" +
                "• Puntos del camino (esferas amarillas): clic y arrastrar con Mover (W). Seleccioná un recorrido para arrastrar todos sus puntos y ver el + que inserta uno entre dos.\n" +
                "• Base, área construible, rocas y pista del tutorial: se mueven igual; radios y tamaños tienen manijas en la escena.\n" +
                "• Rocas nuevas: Ctrl+D sobre una roca, o el botón de abajo.\n" +
                "• Guardar la escena (Ctrl+S) hornea la forma en los datos del nivel. Play prueba este nivel directo.\n" +
                "• Oleadas, nombre y tema: en los datos del nivel (botón de abajo).", MessageType.Info);
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Carriles (el orden de la jerarquía es el número de carril)", EditorStyles.boldLabel);
            var routes = a.Routes();
            for (int i = 0; i < routes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"carril {i}", GUILayout.Width(60));
                EditorGUILayout.LabelField($"{routes[i].name} · {routes[i].points.Count} puntos + base");
                if (GUILayout.Button("Elegir", GUILayout.Width(60))) Selection.activeObject = routes[i].gameObject;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            var errors = LevelBaker.Validate(a);
            if (errors.Count == 0) EditorGUILayout.HelpBox("El nivel valida contra su balance.", MessageType.None);
            else EditorGUILayout.HelpBox("Para corregir:\n• " + string.Join("\n• ", errors), MessageType.Error);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Hornear ahora")) LevelBaker.Bake(a, true);
            if (GUILayout.Button("Datos del nivel")) { if (a.definition != null) Selection.activeObject = a.definition; }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Agregar recorrido")) LevelTools.AddRoute(a);
            if (GUILayout.Button("Agregar roca")) LevelTools.AddRock(a);
            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>Recorrido: lista de puntos y, en la escena, manijas para arrastrar cada punto y botones + para insertar.</summary>
    [CustomEditor(typeof(RouteAuthoring))]
    sealed class RouteAuthoringEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var r = (RouteAuthoring)target;
            EditorGUILayout.HelpBox("Con este recorrido elegido, en la escena: arrastrá las esferas para mover los puntos y tocá un + para insertar uno. " +
                                    "Un punto puede estar en varios recorridos (arrastralo a su lista): así se comparten tramos.", MessageType.Info);
            DrawDefaultInspector();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Agregar punto al final")) LevelTools.AppendPoint(r);
            GUI.enabled = r.points.Count > 1;
            if (GUILayout.Button("Quitar el último")) LevelTools.RemoveLastPoint(r);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        void OnSceneGUI()
        {
            var r = (RouteAuthoring)target;
            var level = r.GetComponentInParent<LevelAuthoring>();
            var b = level != null ? level.Base : null;
            var pts = new List<Vector3>();
            foreach (var p in r.points) if (p != null) pts.Add(p.transform.position);
            if (b != null) pts.Add(b.transform.position);

            // arrastrar cada punto sobre el suelo
            for (int i = 0; i < r.points.Count; i++)
            {
                var w = r.points[i];
                if (w == null) continue;
                var pos = w.transform.position;
                float size = HandleUtility.GetHandleSize(pos) * 0.12f;
                Handles.color = new Color(1f, 0.85f, 0.25f);
                EditorGUI.BeginChangeCheck();
                var np = Handles.Slider2D(pos, Vector3.up, Vector3.right, Vector3.forward, size, Handles.SphereHandleCap, 0.5f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(w.transform, "Mover punto del recorrido");
                    w.transform.position = new Vector3(np.x, 0f, np.z);
                }
                Handles.Label(pos + Vector3.up * 1.1f, i == 0 ? "entrada" : $"{i}");
            }
            // insertar un punto a mitad de un tramo
            for (int i = 1; i < pts.Count; i++)
            {
                var mid = (pts[i] + pts[i - 1]) * 0.5f;
                float size = HandleUtility.GetHandleSize(mid) * 0.08f;
                Handles.color = Color.white;
                if (Handles.Button(mid + Vector3.up * 0.3f, Quaternion.identity, size, size * 1.4f, Handles.CubeHandleCap))
                    LevelTools.InsertPoint(r, i, new Vector3(mid.x, 0f, mid.z));
                Handles.Label(mid + Vector3.up * 0.9f, "+");
            }
        }
    }

    /// <summary>Área construible: manijas de tamaño sobre el suelo.</summary>
    [CustomEditor(typeof(BuildArea))]
    sealed class BuildAreaEditor : Editor
    {
        readonly BoxBoundsHandle handle = new BoxBoundsHandle { axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Z };

        void OnSceneGUI()
        {
            var a = (BuildArea)target;
            handle.center = a.transform.position;
            handle.size = new Vector3(a.size.x, 0f, a.size.y);
            handle.handleColor = new Color(0.45f, 0.95f, 0.55f);
            handle.wireframeColor = new Color(0.45f, 0.95f, 0.55f);
            EditorGUI.BeginChangeCheck();
            handle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(new Object[] { a, a.transform }, "Cambiar el área construible");
                a.size = new Vector2(Mathf.Max(1f, handle.size.x), Mathf.Max(1f, handle.size.z));
                a.transform.position = new Vector3(handle.center.x, 0f, handle.center.z);
            }
        }
    }

    /// <summary>Manija de radio sobre el suelo (rocas, base, pista del tutorial).</summary>
    static class RadiusHandle
    {
        public static void Draw(Object owner, Transform t, ref float radius, Color color, string undo)
        {
            var c = t.position;
            var p = c + Vector3.right * radius;
            Handles.color = color;
            Handles.DrawWireDisc(c + Vector3.up * 0.05f, Vector3.up, radius, 2f);
            EditorGUI.BeginChangeCheck();
            var np = Handles.Slider(p, Vector3.right, HandleUtility.GetHandleSize(p) * 0.1f, Handles.DotHandleCap, 0.05f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(owner, undo);
                radius = Mathf.Max(0.2f, np.x - c.x);
            }
        }
    }

    [CustomEditor(typeof(RockArea))]
    sealed class RockAreaEditor : Editor
    {
        void OnSceneGUI()
        {
            var r = (RockArea)target;
            float v = r.radius;
            RadiusHandle.Draw(r, r.transform, ref v, new Color(1f, 0.45f, 0.4f), "Cambiar el radio de la roca");
            r.radius = v;
        }
    }

    [CustomEditor(typeof(BaseAuthoring))]
    sealed class BaseAuthoringEditor : Editor
    {
        void OnSceneGUI()
        {
            var b = (BaseAuthoring)target;
            float v = b.radius;
            RadiusHandle.Draw(b, b.transform, ref v, new Color(0.4f, 0.8f, 1f), "Cambiar el radio de la base");
            b.radius = v;
        }
    }

    [CustomEditor(typeof(TutorialSpot))]
    sealed class TutorialSpotEditor : Editor
    {
        void OnSceneGUI()
        {
            var s = (TutorialSpot)target;
            float v = s.radius;
            RadiusHandle.Draw(s, s.transform, ref v, new Color(1f, 0.8f, 0.2f), "Cambiar el radio de la pista");
            s.radius = v;
        }
    }
}
