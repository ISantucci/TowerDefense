#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Importa Assets/Data/Catalog/supercell_defenses.json a assets TowerData en
/// Assets/Resources/Towers/&lt;source&gt;/&lt;id&gt;.asset y exporta un CSV de balance.
/// </summary>
public static class SupercellCatalogImporter
{
    const string JsonPath      = "Assets/Data/Catalog/supercell_defenses.json";
    const string CsvPath       = "Assets/Data/Catalog/balance_export.csv";
    const string ResourcesRoot = "Assets/Resources/Towers";

    // ================== IMPORTAR ==================

    [MenuItem("TD/Catálogo Supercell/Importar desde JSON")]
    public static void ImportFromJson()
    {
        if (!File.Exists(JsonPath))
        {
            Debug.LogError("[Catálogo] No existe " + JsonPath);
            return;
        }

        object root;
        try
        {
            root = MiniJson.Parse(File.ReadAllText(JsonPath, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            Debug.LogError("[Catálogo] Error parseando JSON: " + ex.Message);
            return;
        }

        var rootObj = root as Dictionary<string, object>;
        List<object> defenses = null;
        if (rootObj != null && rootObj.ContainsKey("defenses"))
            defenses = rootObj["defenses"] as List<object>;
        else
            defenses = root as List<object>;   // tolera un array top-level

        if (defenses == null)
        {
            Debug.LogError("[Catálogo] El JSON no tiene un array 'defenses'.");
            return;
        }

        Tower archerPrefab, bomberPrefab;
        FindReferencePrefabs(out archerPrefab, out bomberPrefab);

        // Carpetas primero (fuera del batch: CreateAsset dentro de una carpeta recién creada en batch puede fallar).
        EnsureFolder(ResourcesRoot);
        foreach (var item in defenses)
        {
            var e = item as Dictionary<string, object>;
            if (e == null) continue;
            DefenseSource src;
            if (!Enum.TryParse<DefenseSource>(GetString(e, "source"), out src)) src = DefenseSource.Original;
            EnsureFolder(ResourcesRoot + "/" + src);
        }

        int created = 0, updated = 0, skipped = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var item in defenses)
            {
                var e = item as Dictionary<string, object>;
                if (e == null) { skipped++; continue; }

                string idStr = GetString(e, "id");
                TowerId id;
                if (string.IsNullOrEmpty(idStr) || !Enum.TryParse<TowerId>(idStr, out id))
                {
                    Debug.LogWarning("[Catálogo] id desconocido en TowerId, se omite: '" + idStr + "'");
                    skipped++;
                    continue;
                }

                DefenseSource source;
                if (!Enum.TryParse<DefenseSource>(GetString(e, "source"), out source))
                    source = DefenseSource.Original;

                string folder = ResourcesRoot + "/" + source;
                EnsureFolder(folder);
                string assetPath = folder + "/" + id + ".asset";

                var asset = AssetDatabase.LoadAssetAtPath<TowerData>(assetPath);
                bool isNew = asset == null;
                if (isNew)
                {
                    asset = ScriptableObject.CreateInstance<TowerData>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                }

                Fill(asset, e, id, source, archerPrefab, bomberPrefab);
                EditorUtility.SetDirty(asset);

                if (isNew) created++; else updated++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Catálogo] Importación terminada. Creados: " + created + ", actualizados: " + updated + ", omitidos: " + skipped);
    }

    static void Fill(TowerData t, Dictionary<string, object> e, TowerId id, DefenseSource source, Tower archerPrefab, Tower bomberPrefab)
    {
        t.id     = id;
        t.source = source;

        t.displayName = GetString(e, "name");
        string special = GetString(e, "special");
        t.special     = special;
        t.description = special;

        t.kind       = ParseKind(GetString(e, "kind"));
        t.targets    = ParseTargets(GetString(e, "targets"));
        t.attackType = ParseAttackType(GetString(e, "attackType"));

        t.minRange = (float)GetDouble(e, "rangeMin", 0);
        t.range    = (float)GetDouble(e, "rangeMax", 0);
        if (t.minRange > t.range) t.minRange = t.range;

        double attackSpeed = GetDouble(e, "attackSpeedSeconds", 0);
        if (attackSpeed <= 0) attackSpeed = 1;
        t.fireRate = (float)attackSpeed;

        double dps1 = GetDouble(e, "dpsLevel1", 0);
        double perHit = GetDouble(e, "damagePerHitLevel1", double.NaN);
        if (double.IsNaN(perHit)) perHit = dps1 * attackSpeed;
        t.damage = Math.Max(1, (int)Math.Round(perHit, MidpointRounding.AwayFromZero));

        t.splashRadius     = (float)GetDouble(e, "splashRadius", 0);
        t.multiTargetCount = Math.Max(1, GetInt(e, "multiTargetCount", 1));
        t.burstCount       = Math.Max(1, GetInt(e, "burstCount", 1));

        t.hitpoints          = GetInt(e, "hitpointsLevel1", 500);
        t.maxLevelReference  = Math.Max(1, GetInt(e, "maxLevel", 1));
        t.dpsLevel1Reference = (float)dps1;
        t.dpsMaxReference    = (float)GetDouble(e, "dpsMax", 0);
        t.unlockLevel        = Math.Max(1, GetInt(e, "unlockLevel", 1));
        t.referenceBuildCost = GetInt(e, "buildCostLevel1", 0);
        t.referenceCurrency  = GetString(e, "buildCurrency");
        t.statsVerified      = GetBool(e, "verified", false);

        // Costo in-game: round(dps1 * 6 + 40) al múltiplo de 5 más cercano, clamp [40, 600]
        int raw = (int)Math.Round(dps1 * 6 + 40, MidpointRounding.AwayFromZero);
        int rounded = (int)Math.Round(raw / 5.0, MidpointRounding.AwayFromZero) * 5;
        t.cost = Mathf.Clamp(rounded, 40, 600);

        bool explosive = t.attackType == AttackType.Splash || t.attackType == AttackType.Burst;
        t.projectileId = explosive ? ProjectileId.Bomb : ProjectileId.Arrow;
        t.prefab = explosive ? (bomberPrefab != null ? bomberPrefab : archerPrefab)
                             : (archerPrefab != null ? archerPrefab : bomberPrefab);
    }

    static void FindReferencePrefabs(out Tower archer, out Tower bomber)
    {
        archer = null;
        bomber = null;
        Tower archerById = null, bomberById = null;

        foreach (var guid in AssetDatabase.FindAssets("t:TowerData"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var d = AssetDatabase.LoadAssetAtPath<TowerData>(path);
            if (d == null || d.prefab == null) continue;

            if (d.name == "ArcherTowerData") archer = d.prefab;
            else if (d.name == "BomberTowerData") bomber = d.prefab;

            if (d.id == TowerId.Archer && archerById == null) archerById = d.prefab;
            if (d.id == TowerId.Bomber && bomberById == null) bomberById = d.prefab;
        }

        if (archer == null) archer = archerById;
        if (bomber == null) bomber = bomberById;

        if (archer == null && bomber == null)
            Debug.LogWarning("[Catálogo] No se encontró ArcherTowerData / BomberTowerData con prefab; los assets quedarán sin prefab.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string[] parts = path.Split('/');
        string current = parts[0];   // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    // ================== EXPORTAR CSV ==================

    [MenuItem("TD/Catálogo Supercell/Exportar CSV de balance")]
    public static void ExportBalanceCsv()
    {
        var list = new List<TowerData>();

        if (AssetDatabase.IsValidFolder(ResourcesRoot))
        {
            foreach (var guid in AssetDatabase.FindAssets("t:TowerData", new[] { ResourcesRoot }))
            {
                var d = AssetDatabase.LoadAssetAtPath<TowerData>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null) list.Add(d);
            }
        }

        list.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));

        var sb = new StringBuilder();
        sb.AppendLine("id,name,source,dps,range,cost");
        var inv = CultureInfo.InvariantCulture;

        foreach (var d in list)
        {
            sb.Append(d.id).Append(',')
              .Append(Csv(d.DisplayName)).Append(',')
              .Append(d.source).Append(',')
              .Append(d.Dps.ToString("0.##", inv)).Append(',')
              .Append(d.range.ToString("0.##", inv)).Append(',')
              .Append(d.cost.ToString(inv))
              .AppendLine();
        }

        string dir = Path.GetDirectoryName(CsvPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(CsvPath, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log("[Catálogo] CSV exportado (" + list.Count + " filas): " + CsvPath);
    }

    static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    // ================== MAPEOS ==================

    static DefenseKind ParseKind(string s)
    {
        switch ((s ?? "").ToLowerInvariant())
        {
            case "trap":        return DefenseKind.Trap;
            case "hall_weapon": return DefenseKind.HallWeapon;
            case "tower_troop": return DefenseKind.TowerTroop;
            case "building":    return DefenseKind.Building;
            default:            return DefenseKind.Defense;
        }
    }

    static TargetLayer ParseTargets(string s)
    {
        switch ((s ?? "").ToLowerInvariant())
        {
            case "air":  return TargetLayer.Air;
            case "both": return TargetLayer.Both;
            default:     return TargetLayer.Ground;
        }
    }

    static AttackType ParseAttackType(string s)
    {
        switch ((s ?? "").ToLowerInvariant())
        {
            case "splash":  return AttackType.Splash;
            case "multi":   return AttackType.MultiTarget;
            case "burst":   return AttackType.Burst;
            case "beam":    return AttackType.Beam;
            case "chain":   return AttackType.Chain;
            case "push":    return AttackType.Push;
            case "pull":    return AttackType.Pull;
            case "spawner": return AttackType.Spawner;
            case "support": return AttackType.Support;
            case "trap":    return AttackType.Trap;
            default:        return AttackType.SingleTarget;   // "single" o null
        }
    }

    // ================== ACCESO TOLERANTE A NULL ==================

    static string GetString(Dictionary<string, object> e, string key)
    {
        object v;
        if (!e.TryGetValue(key, out v) || v == null) return null;
        return v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture);
    }

    static double GetDouble(Dictionary<string, object> e, string key, double def)
    {
        object v;
        if (!e.TryGetValue(key, out v) || v == null) return def;
        if (v is double) return (double)v;
        if (v is bool) return (bool)v ? 1 : 0;
        double parsed;
        if (v is string && double.TryParse((string)v, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            return parsed;
        return def;
    }

    static int GetInt(Dictionary<string, object> e, string key, int def)
    {
        double d = GetDouble(e, key, double.NaN);
        if (double.IsNaN(d)) return def;
        return (int)Math.Round(d, MidpointRounding.AwayFromZero);
    }

    static bool GetBool(Dictionary<string, object> e, string key, bool def)
    {
        object v;
        if (!e.TryGetValue(key, out v) || v == null) return def;
        if (v is bool) return (bool)v;
        if (v is double) return (double)v != 0;
        if (v is string) return string.Equals((string)v, "true", StringComparison.OrdinalIgnoreCase);
        return def;
    }

    // ================== MINI PARSER JSON ==================
    // object → Dictionary<string,object>, array → List<object>, string, number → double, bool, null.

    class MiniJson
    {
        readonly string s;
        int i;

        MiniJson(string text) { s = text ?? ""; i = 0; }

        public static object Parse(string text)
        {
            var p = new MiniJson(text);
            p.SkipWs();
            object v = p.ParseValue();
            p.SkipWs();
            if (p.i < p.s.Length) throw p.Error("contenido extra al final");
            return v;
        }

        Exception Error(string msg) { return new FormatException("JSON: " + msg + " (pos " + i + ")"); }

        void SkipWs()
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        object ParseValue()
        {
            SkipWs();
            if (i >= s.Length) throw Error("fin inesperado");

            char c = s[i];
            if (c == '{') return ParseObject();
            if (c == '[') return ParseArray();
            if (c == '"') return ParseString();
            if (c == 't') { Expect("true");  return true; }
            if (c == 'f') { Expect("false"); return false; }
            if (c == 'n') { Expect("null");  return null; }
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();

            throw Error("carácter inesperado '" + c + "'");
        }

        void Expect(string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                throw Error("se esperaba '" + word + "'");
            i += word.Length;
        }

        Dictionary<string, object> ParseObject()
        {
            var dict = new Dictionary<string, object>();
            i++; // '{'
            SkipWs();
            if (i < s.Length && s[i] == '}') { i++; return dict; }

            while (true)
            {
                SkipWs();
                if (i >= s.Length || s[i] != '"') throw Error("se esperaba clave string");
                string key = ParseString();
                SkipWs();
                if (i >= s.Length || s[i] != ':') throw Error("se esperaba ':'");
                i++;
                object val = ParseValue();
                dict[key] = val;
                SkipWs();
                if (i >= s.Length) throw Error("objeto sin cerrar");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return dict; }
                throw Error("se esperaba ',' o '}'");
            }
        }

        List<object> ParseArray()
        {
            var list = new List<object>();
            i++; // '['
            SkipWs();
            if (i < s.Length && s[i] == ']') { i++; return list; }

            while (true)
            {
                list.Add(ParseValue());
                SkipWs();
                if (i >= s.Length) throw Error("array sin cerrar");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw Error("se esperaba ',' o ']'");
            }
        }

        string ParseString()
        {
            var sb = new StringBuilder();
            i++; // '"'
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }

                if (i >= s.Length) throw Error("escape incompleto");
                char esc = s[i++];
                switch (esc)
                {
                    case '"':  sb.Append('"');  break;
                    case '\\': sb.Append('\\'); break;
                    case '/':  sb.Append('/');  break;
                    case 'b':  sb.Append('\b'); break;
                    case 'f':  sb.Append('\f'); break;
                    case 'n':  sb.Append('\n'); break;
                    case 'r':  sb.Append('\r'); break;
                    case 't':  sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw Error("\\u incompleto");
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: throw Error("escape inválido '\\" + esc + "'");
                }
            }
            throw Error("string sin cerrar");
        }

        object ParseNumber()
        {
            int start = i;
            if (s[i] == '-') i++;
            while (i < s.Length)
            {
                char c = s[i];
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') i++;
                else break;
            }

            string num = s.Substring(start, i - start);
            double d;
            if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw Error("número inválido '" + num + "'");
            return d;
        }
    }
}
#endif
