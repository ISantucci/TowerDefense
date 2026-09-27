using System.IO;
using System.Text.Json;
using ClashDefense.Core;

namespace ClashDefense.Sim
{
    static class DataLoader
    {
        static readonly JsonSerializerOptions Opt = new JsonSerializerOptions
        {
            IncludeFields = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

        public static string DataDir = Path.GetFullPath(Path.Combine(System.AppContext.BaseDirectory, "../../../../../Assets/ClashDefense/Data"));

        public static BalanceData Balance(string file = "balance_p0.json") => JsonSerializer.Deserialize<BalanceData>(File.ReadAllText(Path.Combine(DataDir, file)), Opt);
        public static LevelData Level(string file = "level_p0.json") => JsonSerializer.Deserialize<LevelData>(File.ReadAllText(Path.Combine(DataDir, file)), Opt);
        public static string ToJson<T>(T obj) => JsonSerializer.Serialize(obj, Opt);
        public static T Clone<T>(T obj) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj, Opt), Opt);
    }
}
