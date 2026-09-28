using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace ClashDefense.QA
{
    /// <summary>
    /// Medición de rendimiento para comparar antes y después (Vaultrum Core: Medir antes de optimizar · Comparación antes y
    /// después). Muestrea cuadro a cuadro el tiempo de cuadro, la basura del GC y las llamadas de dibujo con ProfilerRecorder,
    /// que funciona en el editor y en builds de desarrollo sin abrir el Profiler. Solo QA: nunca corre en la build.
    /// </summary>
    public sealed class PerfSampler : IDisposable
    {
        ProfilerRecorder gcBytes, gcCount, drawCalls, batches, setPass;
        readonly List<float> frameMs = new List<float>();
        readonly List<long> gcPerFrame = new List<long>();
        readonly List<long> gcCountPerFrame = new List<long>();
        readonly List<long> draws = new List<long>();
        readonly List<long> batch = new List<long>();
        readonly List<long> passes = new List<long>();

        public PerfSampler()
        {
            gcBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            gcCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
            drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        }

        /// <summary>Llamar una vez por cuadro, después del cuadro que se quiere medir.</summary>
        public void Frame()
        {
            frameMs.Add(Time.unscaledDeltaTime * 1000f);
            gcPerFrame.Add(gcBytes.Valid ? gcBytes.LastValue : -1);
            gcCountPerFrame.Add(gcCount.Valid ? gcCount.LastValue : -1);
            long dc = drawCalls.Valid ? drawCalls.LastValue : -1, bt = batches.Valid ? batches.LastValue : -1;
#if UNITY_EDITOR
            // en el editor esos contadores no llegan por ProfilerRecorder (orden 051): se leen de las estadísticas de la Game view
            if (dc <= 0) dc = UnityEditor.UnityStats.drawCalls;
            if (bt <= 0) bt = (UnityEditor.UnityStats.staticBatchedDrawCalls - UnityEditor.UnityStats.staticBatches)
                            + (UnityEditor.UnityStats.dynamicBatchedDrawCalls - UnityEditor.UnityStats.dynamicBatches)
                            + (UnityEditor.UnityStats.instancedBatchedDrawCalls - UnityEditor.UnityStats.instancedBatches);
#endif
            draws.Add(dc);
            batch.Add(bt);
            passes.Add(setPass.Valid ? setPass.LastValue : -1);
        }

        public int Frames => frameMs.Count;

        static string Stat(List<float> v)
        {
            if (v.Count == 0) return "sin datos";
            var s = v.OrderBy(x => x).ToList();
            float p95 = s[Mathf.Clamp((int)(s.Count * 0.95f), 0, s.Count - 1)];
            return $"prom {s.Average():0.00} · p95 {p95:0.00} · máx {s[s.Count - 1]:0.00}";
        }

        static string Stat(List<long> v, string unit = "")
        {
            if (v.Count == 0 || v.All(x => x < 0)) return "no disponible";
            var s = v.Where(x => x >= 0).OrderBy(x => x).ToList();
            long p95 = s[Mathf.Clamp((int)(s.Count * 0.95f), 0, s.Count - 1)];
            return $"prom {s.Average():0} · p95 {p95} · máx {s[s.Count - 1]}{unit}";
        }

        public string Report(string title)
        {
            var sb = new StringBuilder();
            sb.Append(title).Append('\n');
            sb.Append($"cuadros muestreados      {Frames}\n");
            sb.Append($"tiempo de cuadro (ms)    {Stat(frameMs)}\n");
            sb.Append($"GC por cuadro (bytes)    {Stat(gcPerFrame)}\n");
            sb.Append($"GC por cuadro (asign.)   {Stat(gcCountPerFrame)}\n");
            sb.Append($"draw calls (batches)     {Stat(draws)}\n");
            sb.Append($"ahorradas por batching   {Stat(batch)}\n");
            sb.Append($"SetPass calls            {Stat(passes)}\n");
            return sb.ToString();
        }

        /// <summary>Recursos vivos: un material o una malla que crece partida a partida es una fuga (Vaultrum: Memory Leak).</summary>
        public static string Census()
        {
            int mats = Resources.FindObjectsOfTypeAll<Material>().Length;
            int meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            int gos = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            return $"materiales {mats} · mallas {meshes} · objetos en escena {gos} · canvases {canvases}";
        }

        public void Dispose()
        {
            gcBytes.Dispose(); gcCount.Dispose(); drawCalls.Dispose(); batches.Dispose(); setPass.Dispose();
        }
    }
}
