using System.Collections.Generic;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Reserva de instancias de un prefab (Vaultrum Core: Instantiate y destroy constantes). Durante la partida los enemigos,
    /// proyectiles y efectos se piden y se devuelven; se crean solo cuando la reserva está vacía y nunca se destruyen hasta
    /// que se cierra el juego. Orden recomendado por Unity: al devolver, primero se desactiva y después se reubica.
    /// </summary>
    public sealed class PrefabPool<T> where T : Component
    {
        readonly T prefab;
        readonly Transform parent;
        readonly Stack<T> free = new Stack<T>();

        public int Created { get; private set; }

        public PrefabPool(T prefab, Transform parent)
        {
            this.prefab = prefab;
            this.parent = parent;
        }

        public T Get()
        {
            T x;
            if (free.Count > 0) x = free.Pop();
            else { x = Object.Instantiate(prefab, parent); Created++; }
            x.gameObject.SetActive(true);
            return x;
        }

        public void Release(T x)
        {
            if (x == null) return;
            x.gameObject.SetActive(false);
            if (x.transform.parent != parent) x.transform.SetParent(parent, false);
            free.Push(x);
        }

        public void Prewarm(int count)
        {
            var tmp = new List<T>();
            for (int i = 0; i < count; i++) tmp.Add(Get());
            foreach (var x in tmp) Release(x);
        }
    }
}
