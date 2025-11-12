using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class DataTrace
{
    // activá/desactivá logs sin tocar demás scripts
    public static bool Enabled = true;

    public static void LogQueue<T>(string title, Queue<T> q)
    {
        if (!Enabled || q == null) return;
        var arr = q.ToArray();
        var sb = new StringBuilder();
        sb.AppendLine($"[TRACE][QUEUE] {title} | count={arr.Length}");
        for (int i = 0; i < arr.Length; i++)
            sb.AppendLine($"  idx={i}: {arr[i]}");
        Debug.Log(sb.ToString());
    }

    public static void LogStacks<T>(string title, Stack<T> undo, Stack<T> redo)
    {
        if (!Enabled) return;
        var sb = new StringBuilder();
        sb.AppendLine($"[TRACE][STACKS] {title}");
        sb.AppendLine($"  UNDO count={undo?.Count ?? 0}");
        if (undo != null && undo.Count > 0)
            sb.AppendLine($"    UNDO top={undo.Peek()}");
        sb.AppendLine($"  REDO count={redo?.Count ?? 0}");
        if (redo != null && redo.Count > 0)
            sb.AppendLine($"    REDO top={redo.Peek()}");
        Debug.Log(sb.ToString());
    }

    public static void LogEvent(string title, string detail = null)
    {
        if (!Enabled) return;
        Debug.Log($"[TRACE][EVT] {title}" + (string.IsNullOrEmpty(detail) ? "" : $" | {detail}"));
    }
}
