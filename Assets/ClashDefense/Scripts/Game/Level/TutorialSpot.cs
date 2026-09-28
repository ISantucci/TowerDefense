using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>Zona que el tutorial sugiere en el paso 5 (LDS-001.1): donde el camino pasa dos veces por el alcance.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Clash Defense/Nivel/Pista del tutorial")]
    public sealed class TutorialSpot : MonoBehaviour
    {
        [Min(0.2f)] public float radius = 2f;

        public CircleData ToCircle()
        {
            var c = LevelAuthoring.Flat(transform.position);
            return new CircleData { x = c.x, z = c.z, radius = LevelAuthoring.Round(radius) };
        }
    }
}
