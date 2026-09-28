using System.Collections.Generic;
using ClashDefense.Core;
using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>Un punto de recorrido. Un mismo punto puede estar en varios recorridos: así se arman las bifurcaciones.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Clash Defense/Nivel/Punto de recorrido")]
    public sealed class Waypoint : MonoBehaviour
    {
        public Vec2 Flat => LevelAuthoring.Flat(transform.position);
    }
}
