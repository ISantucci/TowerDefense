using System;
using UnityEngine;

/// <summary>
/// Pila por índices sobre arreglo dinámico (duplica capacidad cuando se llena).
/// Cumple TDA Pila usando 'top' como índice del próximo slot libre.
/// </summary>
public class ArrayStack<T>
{
    private T[] data;
    private int top; // índice del próximo slot libre (también = cantidad actual)

    public ArrayStack(int initialCapacity = 16)
    {
        if (initialCapacity < 1) initialCapacity = 1;
        data = new T[initialCapacity];
        top = 0;
    }

    public int Count => top;
    public int Capacity => data.Length;
    public bool IsEmpty => top == 0;

    public void Push(T item)
    {
        if (top >= data.Length) Grow();
        data[top] = item;
        top++;
        // TRACE opcional aquí si querés: Debug.Log($"[ArrayStack] Push -> top={top}, cap={Capacity}");
    }

    public T Pop()
    {
        if (top == 0) throw new InvalidOperationException("Pila vacía");
        top--;
        T item = data[top];
        data[top] = default; // limpia referencia
        // TRACE opcional: Debug.Log($"[ArrayStack] Pop -> top={top}, cap={Capacity}");
        return item;
    }

    public T Peek()
    {
        if (top == 0) throw new InvalidOperationException("Pila vacía");
        return data[top - 1];
    }

    public void Clear()
    {
        Array.Clear(data, 0, top);
        top = 0;
    }

    private void Grow()
    {
        int newCap = data.Length * 2;
        var nd = new T[newCap];
        Array.Copy(data, nd, data.Length);
        data = nd;
    }
}
