using UnityEngine;

public class BuildInvoker : MonoBehaviour
{
    private CommandStack undoStack = new CommandStack();
    private CommandStack redoStack = new CommandStack();

    private const int MaxHistory = 3;

    private void Awake()
    {
        undoStack.Initialize(MaxHistory);
        redoStack.Initialize(MaxHistory);
    }

    public void Do(ICommand cmd)
    {
        if (cmd == null) return;

        cmd.Execute();

        if (cmd is PlaceTowerCommand ptc && !ptc.IsDone)
            return;

        undoStack.Push(cmd);
        redoStack.Clear();
    }

    public void Undo()
    {
        if (undoStack.IsEmpty()) return;

        ICommand cmd = undoStack.Pop();
        if (cmd == null) return;

        cmd.Undo();
        redoStack.Push(cmd);
    }

    public void Redo()
    {
        if (redoStack.IsEmpty()) return;

        ICommand cmd = redoStack.Pop();
        if (cmd == null) return;

        cmd.Execute();
        undoStack.Push(cmd);
    }

    public void ClearHistory()
    {
        undoStack.Clear();
        redoStack.Clear();
    }

    public bool CanUndo => !undoStack.IsEmpty();
    public bool CanRedo => !redoStack.IsEmpty();
}
