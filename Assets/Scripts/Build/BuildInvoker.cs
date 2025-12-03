using System.Collections.Generic;
using UnityEngine;

public class BuildInvoker : MonoBehaviour
{
    readonly Stack<ICommand> undoStack = new();
    readonly Stack<ICommand> redoStack = new();

    public bool CanUndo => undoStack.Count > 0;
    public bool CanRedo => redoStack.Count > 0;

    public void Do(ICommand cmd)
    {
        cmd.Execute();

       if (cmd is PlaceTowerCommand ptc && !ptc.IsDone) return;

        undoStack.Push(cmd);
        redoStack.Clear();
    }

    public void Undo()
    {
        if (undoStack.Count == 0) return;

        var c = undoStack.Pop();
        c.Undo();
        redoStack.Push(c);

        Debug.Log("[Invoker] Undo ejecutado.");
    }

    public void Redo()
    {
        if (redoStack.Count == 0) return;

        var c = redoStack.Pop();
        c.Execute();
        undoStack.Push(c);

        Debug.Log("[Invoker] Redo ejecutado.");
    }

    public void ClearHistory()
    {
        undoStack.Clear();
        redoStack.Clear();
        Debug.Log("[Invoker] Historial limpiado.");
    }
}
