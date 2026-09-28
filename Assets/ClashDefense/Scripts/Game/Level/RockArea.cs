using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>Zona bloqueada: roca del continente. Ahí no se construye.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Clash Defense/Nivel/Roca (zona bloqueada)")]
    public sealed class RockArea : MonoBehaviour
    {
        [Min(0.2f)] public float radius = 1.5f;
        [Tooltip("Dibujar la roca con primitivas. Apagalo cuando pongas un modelo propio como hijo.")]
        public bool generateVisual = true;

        public CircleData ToCircle()
        {
            var c = LevelAuthoring.Flat(transform.position);
            return new CircleData { x = c.x, z = c.z, radius = LevelAuthoring.Round(radius) };
        }
    }
}
