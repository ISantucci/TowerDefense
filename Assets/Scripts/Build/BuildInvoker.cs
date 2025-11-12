using UnityEngine;

public class BuildInvoker : MonoBehaviour
{
    // Pilas por índices (TDA) para Undo/Redo
    private readonly ArrayStack<ICommand> undoStack = new(32);
    private readonly ArrayStack<ICommand> redoStack = new(32);

    public void Do(ICommand cmd)
    {
        cmd.Execute();

        // Si es PlaceTowerCommand y no se completó, no apilo
        if (cmd is PlaceTowerCommand ptc && !ptc.IsDone)
        {
            Debug.Log($"[BuildInvoker] Do(cancel) -> IsDone=false | undoCount={undoStack.Count}, redoCount={redoStack.Count}");
            return;
        }

        undoStack.Push(cmd);
        redoStack.Clear();

        Debug.Log($"[BuildInvoker] Do(push) -> undoTop={undoStack.Count} | redo=0 | topCmd={TopName(undoStack)}");
    }

    public void Undo()
    {
        if (undoStack.IsEmpty)
        {
            Debug.Log("[BuildInvoker] Undo -> pila vacía");
            return;
        }

        var c = undoStack.Pop();
        c.Undo();
        redoStack.Push(c);

        Debug.Log($"[BuildInvoker] Undo(pop->push) -> undoTop={undoStack.Count}, redoTop={redoStack.Count} | redoTopCmd={TopName(redoStack)}");
    }

    public void Redo()
    {
        if (redoStack.IsEmpty)
        {
            Debug.Log("[BuildInvoker] Redo -> pila vacía");
            return;
        }

        var c = redoStack.Pop();
        c.Execute();

        // Si fuera PlaceTowerCommand y falló al re-ejecutar, no lo apilo
        if (c is PlaceTowerCommand ptc && !ptc.IsDone)
        {
            Debug.Log($"[BuildInvoker] Redo(cancel) -> IsDone=false | undoTop={undoStack.Count}, redoTop={redoStack.Count}");
            return;
        }

        undoStack.Push(c);

        Debug.Log($"[BuildInvoker] Redo(pop->push) -> undoTop={undoStack.Count}, redoTop={redoStack.Count} | undoTopCmd={TopName(undoStack)}");
    }

    private static string TopName(ArrayStack<ICommand> s)
    {
        try
        {
            var top = s.Peek();
            return top?.GetType().Name ?? "(null)";
        }
        catch
        {
            return "(empty)";
        }
    }
}
