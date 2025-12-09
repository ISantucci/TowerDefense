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


    public void Enqueue(T item)
    {
        // si el array es null, inicializamos
        if (items == null)
        {
            items = new T[1];
            items[0] = item;
            count = 1;
            return;
        }

        // si hay espacio libre al final
        if (count < items.Length)
        {
            items[count] = item;
            count++;
            return;
        }

        // si está llena, agrandamos en 1 (simple, suficiente para el TP)
        T[] nuevo = new T[count + 1];
        for (int i = 0; i < count; i++)
            nuevo[i] = items[i];

        nuevo[count] = item;
        items = nuevo;
        count++;
    }

    public T Dequeue()
    {
        if (IsEmpty()) return default;

        T first = items[0];

        // corrimiento a la izquierda
        for (int i = 1; i < count; i++)
            items[i - 1] = items[i];

        count--;
        return first;
    }


    public int Count => count;
}
