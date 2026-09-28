using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>Proyectil de una torre (flecha, bala, bomba, orbe). Va en su prefab; se recicla entre disparos.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Clash Defense/Presentación/Proyectil")]
    public sealed class ProjectileVisual : MonoBehaviour
    {
        [Tooltip("Gira para mirar hacia donde va (flechas). Las esferas no lo necesitan.")]
        [SerializeField] internal bool faceDirection = true;
        public bool FaceDirection => faceDirection;
    }
}
