using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ClashDefense.EditorTools;
using ClashDefense.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClashDefense.Tests
{
    /// <summary>
    /// Pruebas de los assets (TL-003): que las escenas de nivel, sus datos horneados, los JSON del simulador y los prefabs
    /// estén de acuerdo entre sí, y que ningún prefab tenga una referencia vacía (lo que antes fallaba en el Play ahora
    /// falla acá, con el nombre del campo).
    /// </summary>
    public class AssetTests
    {
        static GameConfig Cfg() => CampaignTests.Cfg();

        static List<LevelDefinition> AllLevels()
        {
            var c = Cfg();
            var l = c.campaign.Levels().ToList();
            l.Add(c.labLevel);
            return l;
        }

        [Test] public void ElJuegoTieneSuConfiguracion()
        {
            var c = Cfg();
            Assert.IsNotNull(c, "falta Assets/ClashDefense/Data/Juego_Mundo1.asset");
            Assert.IsNotNull(c.campaign); Assert.IsNotNull(c.labBalance); Assert.IsNotNull(c.labLevel); Assert.IsNotNull(c.presentation);
            Assert.IsEmpty(c.campaign.Validate(), string.Join(" | ", c.campaign.Validate()));
        }

        [Test] public void CadaNivelTieneEscenaYFormaHorneadaQueValida()
        {
            var errs = new List<string>();
            foreach (var l in AllLevels())
            {
                if (string.IsNullOrEmpty(l.ScenePath) || !File.Exists(l.ScenePath)) errs.Add($"{l.name}: sin escena ({l.ScenePath})");
                foreach (var e in l.Validate()) errs.Add($"{l.name}: {e}");
            }
            Assert.IsEmpty(errs, string.Join(" | ", errs));
        }

        [Test] public void LoHorneadoEsLoQueHayEnCadaEscena()
        {
            var errs = new List<string>();
            foreach (var l in AllLevels())
            {
                var already = EditorSceneManager.GetSceneByPath(l.ScenePath);
                bool opened = !(already.IsValid() && already.isLoaded);
                var scene = opened ? EditorSceneManager.OpenScene(l.ScenePath, OpenSceneMode.Additive) : already;
                var a = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelAuthoring>(true)).FirstOrDefault();
                if (a == null) errs.Add($"{l.name}: la escena no tiene LevelAuthoring");
                else if (a.definition != l) errs.Add($"{l.name}: la escena apunta a otros datos ({a.definition?.name})");
                else if (JsonUtility.ToJson(a.BuildShape()) != JsonUtility.ToJson(l.Shape)) errs.Add($"{l.name}: la escena cambió y no se horneó (guardala)");
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            Assert.IsEmpty(errs, string.Join(" | ", errs));
        }

        [Test] public void LosJsonDelSimuladorEstanAlDia()
        {
            var reference = new Dictionary<string, string>();
            foreach (var f in Directory.GetFiles(DataExporter.OutDir, "*.json")) reference[Path.GetFileName(f)] = File.ReadAllText(f);
            var cmp = DataExporter.Compare(reference, DataExporter.Files(Cfg()));
            var bad = cmp.Where(x => !x.EndsWith(": igual")).ToList();
            Assert.IsEmpty(bad, "exportá los datos (Clash Defense/Datos/Exportar): " + string.Join(" | ", bad));
        }

        [Test] public void CadaTorreYEnemigoTienePrefab()
        {
            var errs = new List<string>();
            foreach (var b in new[] { Cfg().campaign.balance, Cfg().labBalance })
            {
                foreach (var t in b.towers) if (t.prefab == null) errs.Add($"{b.name}/{t.name}: sin prefab");
                foreach (var e in b.enemies) if (e.prefab == null) errs.Add($"{b.name}/{e.name}: sin prefab");
            }
            Assert.IsEmpty(errs, string.Join(" | ", errs));
        }

        static readonly HashSet<string> Optional = new HashSet<string>
        {
            "TowerVisual.aim", "TowerVisual.orb", "TowerVisual.coins", "TowerVisual.fullMark",
            "EnemyVisual.armorFill", "EnemyVisual.armorBar", "EnemyVisual.shell", "EnemyVisual.flame",
            "RingFx.under", "GameConfig.campaign",
        };

        /// <summary>Campos serializados que apuntan a objetos de Unity y quedaron vacíos (salvo los opcionales).</summary>
        static IEnumerable<string> Missing(Object owner, string where)
        {
            for (var type = owner.GetType(); type != null && type != typeof(MonoBehaviour) && type != typeof(ScriptableObject); type = type.BaseType)
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                    if (Optional.Contains(type.Name + "." + f.Name)) continue;
                    var v = f.GetValue(owner);
                    if (typeof(Object).IsAssignableFrom(f.FieldType))
                    {
                        if ((v as Object) == null) yield return $"{where}: {type.Name}.{f.Name} vacío";
                    }
                    else if (f.FieldType.IsArray && typeof(Object).IsAssignableFrom(f.FieldType.GetElementType()))
                    {
                        var arr = (System.Array)v;
                        if (arr == null) continue;
                        for (int i = 0; i < arr.Length; i++) if ((arr.GetValue(i) as Object) == null) yield return $"{where}: {type.Name}.{f.Name}[{i}] vacío";
                    }
                }
        }

        [Test] public void NingunPrefabTieneReferenciasVacias()
        {
            var errs = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ClashDefense" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) { errs.Add($"{path}: script faltante"); continue; }
                    var ns = mb.GetType().Namespace;
                    if (ns == null || !ns.StartsWith("ClashDefense")) continue;
                    errs.AddRange(Missing(mb, $"{Path.GetFileNameWithoutExtension(path)}/{mb.name}"));
                }
            }
            errs.AddRange(Missing(Cfg(), "Juego_Mundo1"));
            errs.AddRange(Missing(Cfg().presentation, "Presentacion"));
            Assert.IsEmpty(errs, string.Join(" | ", errs));
        }
    }
}
