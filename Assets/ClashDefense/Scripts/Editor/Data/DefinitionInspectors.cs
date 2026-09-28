using System.Collections.Generic;
using ClashDefense.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClashDefense.EditorTools
{
    /// <summary>Validación a la vista: arriba de cada dato del juego, en rojo lo que hay que corregir (Doc 05 §1: los números son hipótesis que se cambian seguido).</summary>
    static class ValidationBox
    {
        public static void Draw(List<string> errors, string okText)
        {
            if (errors == null || errors.Count == 0) EditorGUILayout.HelpBox(okText, MessageType.None);
            else EditorGUILayout.HelpBox("Para corregir:\n• " + string.Join("\n• ", errors), MessageType.Error);
        }
    }

    [CustomEditor(typeof(BalanceDefinition))]
    sealed class BalanceDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var b = (BalanceDefinition)target;
            EditorGUILayout.HelpBox("Balance: economía, tiempos, estrellas y qué torres y enemigos existen. Cada número de una torre o un enemigo se edita en su propio asset (doble clic en la lista). " +
                                    "Si cambiás un número, subí la versión y anotalo en CHANGELOG_balance.md.", MessageType.Info);
            ValidationBox.Draw(b.Validate(), "El balance valida.");
            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(CampaignDefinition))]
    sealed class CampaignDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var c = (CampaignDefinition)target;
            EditorGUILayout.HelpBox("Campaña: mundos, continentes y niveles (cada nivel apunta a sus datos), moneda, tienda y mejora de tanda.", MessageType.Info);
            ValidationBox.Draw(c.Validate(), "La campaña valida.");
            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(LevelDefinition))]
    sealed class LevelDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var l = (LevelDefinition)target;
            EditorGUILayout.HelpBox("Datos del nivel: nombre, tema, oleadas. La forma (caminos, rocas, base, área) se edita en su escena y se hornea acá al guardarla.", MessageType.Info);
            ValidationBox.Draw(l.Validate(), "El nivel valida contra su balance.");
            GUI.enabled = !string.IsNullOrEmpty(l.ScenePath);
            if (GUILayout.Button("Abrir la escena del nivel") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(l.ScenePath);
            GUI.enabled = true;
            EditorGUILayout.LabelField("Escena", string.IsNullOrEmpty(l.ScenePath) ? "(sin escena)" : l.ScenePath);
            var s = l.Shape;
            if (s != null && !s.IsEmpty)
                EditorGUILayout.LabelField("Forma horneada", $"{s.routes.Length} recorridos · {s.blocked.Length} rocas · área {s.buildArea.maxX - s.buildArea.minX:0.#}×{s.buildArea.maxZ - s.buildArea.minZ:0.#} · base r {s.baseRadius:0.##}");
            DrawDefaultInspector();
            if (l.waves != null && l.waves.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Resumen de oleadas", EditorStyles.boldLabel);
                for (int i = 0; i < l.waves.Count; i++) EditorGUILayout.LabelField($"oleada {i + 1}", LevelDefinition.WaveSummary(l.waves[i]));
            }
        }
    }

    [CustomEditor(typeof(TowerDefinition))]
    sealed class TowerDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var t = (TowerDefinition)target;
            EditorGUILayout.HelpBox("Torre: 'data' son sus reglas y números (N1 y N2). El prefab es cómo se ve: se puede reemplazar por arte sin tocar código " +
                                    "(las partes animadas se reasignan en el componente Torre del prefab).", MessageType.Info);
            var e = new List<string>();
            if (t.data == null || string.IsNullOrEmpty(t.data.id)) e.Add("falta el id");
            if (t.data != null && (t.data.levels == null || t.data.levels.Length == 0)) e.Add("no tiene niveles (N1, N2)");
            if (t.prefab == null) e.Add("falta el prefab");
            ValidationBox.Draw(e, "La torre tiene lo necesario (la validación completa está en su balance).");
            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(EnemyDefinition))]
    sealed class EnemyDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var t = (EnemyDefinition)target;
            EditorGUILayout.HelpBox("Enemigo: 'data' son sus números; 'code' es la letra que usan las oleadas. El prefab es cómo se ve.", MessageType.Info);
            var e = new List<string>();
            if (t.data == null || string.IsNullOrEmpty(t.data.id)) e.Add("falta el id");
            if (t.data != null && string.IsNullOrEmpty(t.data.code)) e.Add("falta la letra (code) de las oleadas");
            if (t.prefab == null) e.Add("falta el prefab");
            ValidationBox.Draw(e, "El enemigo tiene lo necesario (la validación completa está en su balance).");
            DrawDefaultInspector();
        }
    }
}
