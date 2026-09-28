using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>Rectángulo donde se puede construir (sobre el suelo, centrado en este objeto).</summary>
    [DisallowMultipleComponent, AddComponentMenu("Clash Defense/Nivel/Área construible")]
    public sealed class BuildArea : MonoBehaviour
    {
        [Tooltip("Ancho (X) y profundidad (Z) del área, en unidades de mundo.")]
        public Vector2 size = new Vector2(40f, 22f);

        public RectData ToRect()
        {
            var c = LevelAuthoring.Flat(transform.position);
            return new RectData
            {
                minX = LevelAuthoring.Round(c.x - size.x * 0.5f), maxX = LevelAuthoring.Round(c.x + size.x * 0.5f),
                minZ = LevelAuthoring.Round(c.z - size.y * 0.5f), maxZ = LevelAuthoring.Round(c.z + size.y * 0.5f),
            };
        }
    }
}
