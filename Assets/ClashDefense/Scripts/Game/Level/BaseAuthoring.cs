using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>La base que se defiende. Todos los recorridos terminan acá.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Clash Defense/Nivel/Base")]
    public sealed class BaseAuthoring : MonoBehaviour
    {
        [Min(0.5f), Tooltip("Radio de la base: los enemigos que llegan a este círculo le hacen daño.")]
        public float radius = 1.6f;
        [Tooltip("Dibujar la base con primitivas. Apagalo cuando pongas un modelo propio como hijo.")]
        public bool generateVisual = true;
    }
}
