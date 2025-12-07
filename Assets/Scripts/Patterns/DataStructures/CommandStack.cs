public class CommandStack
{
    private ICommand[] items;
    private int maxCount;
    private int index;

    public void Initialize(int capacity)
    {
        maxCount = capacity;
        items = new ICommand[capacity];
        index = 0;
    }

    public bool IsEmpty()
    {
        return index == 0;
    }

    public bool IsFull()
    {
        return index == maxCount;
    }

    public void Push(ICommand cmd)
    {
        if (!IsFull())
        {
            items[index] = cmd;
            index++;
        }
        else
        {
            for (int i = 1; i < maxCount; i++)
                items[i - 1] = items[i];

            items[maxCount - 1] = cmd;
        }
    }

    public ICommand Pop()
    {
        if (IsEmpty()) return null;

        index--;
        ICommand cmd = items[index];
        items[index] = null;
        return cmd;
    }

    public ICommand Peek()
    {
        if (IsEmpty()) return null;
        return items[index - 1];
    }

    public void Clear()
    {
        for (int i = 0; i < index; i++)
            items[i] = null;

        index = 0;
    }
}
