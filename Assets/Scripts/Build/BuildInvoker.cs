// Assets/Scripts/Build/BuildInvoker.cs
using System.Collections.Generic;
using System.Windows.Input;
using UnityEngine;

public class BuildInvoker : MonoBehaviour
{
    private readonly Stack<ICommand> undoStack = new();
    private readonly Stack<ICommand> redoStack = new();

    // BuildInvoker.cs
    public void Do(ICommand cmd)
    {
        cmd.Execute();

        // Solo apilo si el comando se completó
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
    }

    public void Redo()
    {
        if (redoStack.Count == 0) return;
        var c = redoStack.Pop();
        c.Execute();
        undoStack.Push(c);
    }
}

