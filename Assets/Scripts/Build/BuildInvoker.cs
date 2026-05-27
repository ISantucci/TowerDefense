using UnityEngine;

public class BuildInvoker : MonoBehaviour
{
    [SerializeField] int maxHistory = 3;

    CommandStack undoStack = new CommandStack();
    CommandStack redoStack = new CommandStack();

    void Awake()
    {
        undoStack.Initialize(maxHistory);
        redoStack.Initialize(maxHistory);
    }

    public void Do(ICommand cmd)
    {
        if (cmd == null) return;

        int cost = GetCost(cmd);

        if (cost > 0)
        {
            if (GameManager.I == null) return;
            if (GameManager.I.Money < cost) return;

            EventQueueManager.Enqueue(GameplayEvent.AddMoney(-cost));
        }

        cmd.Execute();

        if (cmd is ICostCommand cc && !cc.IsDone)
        {
            if (cost > 0)
                EventQueueManager.Enqueue(GameplayEvent.AddMoney(cost));
            return;
        }

        undoStack.Push(cmd);
        redoStack.Clear();
    }

    public void Undo()
    {
        if (undoStack.IsEmpty()) return;

        ICommand cmd = undoStack.Pop();
        if (cmd == null) return;

        cmd.Undo();

        if (cmd is ICostCommand cc)
        {
            if (cc.IsDone)
            {
                // Undo falló — IsDone sigue true; no reembolsar, restaurar al stack
                undoStack.Push(cmd);
                return;
            }
            if (cc.Cost > 0)
                EventQueueManager.Enqueue(GameplayEvent.AddMoney(cc.Cost));
        }

        redoStack.Push(cmd);
    }

    public void Redo()
    {
        // Redo deshabilitado — solo existe para no romper referencias existentes
    }

    public void ClearHistory()
    {
        undoStack.Clear();
        redoStack.Clear();
    }

    int GetCost(ICommand cmd) => cmd is ICostCommand cc ? cc.Cost : 0;



    class CommandStack
    {
        ICommand[] a;
        int max;
        int index;

        public void Initialize(int capacity)
        {
            max = Mathf.Max(1, capacity);
            a = new ICommand[max];
            index = 0;
        }

        public bool IsEmpty() => index == 0;

        public void Push(ICommand cmd)
        {
            if (cmd == null) return;

            if (index < max)
            {
                a[index] = cmd;
                index++;
                return;
            }

            for (int i = 1; i < max; i++)
                a[i - 1] = a[i];

            a[max - 1] = cmd;
        }

        public ICommand Pop()
        {
            if (IsEmpty()) return null;

            index--;
            ICommand cmd = a[index];
            a[index] = null;
            return cmd;
        }

        public void Clear()
        {
            for (int i = 0; i < index; i++)
                a[i] = null;
            index = 0;
        }

        }

    public bool CanUndo => !undoStack.IsEmpty();
    public bool CanRedo => !redoStack.IsEmpty();

}
