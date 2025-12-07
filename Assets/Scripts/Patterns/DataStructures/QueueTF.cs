public class QueueTF<T>
{
    private T[] items;
    private int count;

    public void InitializeFromArray(T[] source)
    {
        if (source == null)
        {
            items = new T[0];
            count = 0;
            return;
        }

        items = new T[source.Length];
        for (int i = 0; i < source.Length; i++)
            items[i] = source[i];

        count = source.Length;
    }

    public bool IsEmpty()
    {
        return count == 0;
    }

    public T First()
    {
        if (count == 0) return default;
        return items[0];
    }

    public void Dequeue()
    {
        if (count == 0) return;

        for (int i = 1; i < count; i++)
            items[i - 1] = items[i];

        items[count - 1] = default;
        count--;
    }

    public int Count => count;
}
