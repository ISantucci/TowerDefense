using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>Prefabs y materiales de los efectos de la partida (UXS-001.x, UXS-002.4/.5). Se reemplazan por arte sin tocar código.</summary>
    [CreateAssetMenu(menuName = "Clash Defense/Biblioteca de presentación", fileName = "Presentacion", order = 40)]
    public sealed class PresentationLibrary : ScriptableObject
    {
        [Tooltip("Material transparente del fantasma de colocación y de los fundidos.")]
        public Material ghostMaterial;
        [Tooltip("Material de las líneas (anillos, rayos).")]
        public Material lineMaterial;
        [Header("Efectos (se reciclan: no se crean ni se destruyen durante la partida)")]
        public FloatingText floatingText;
        public RingFx ring;
        public MeshFx spark;
        public MeshFx flameBurst;
        public MeshFx fireOnGround;
        public MeshFx armorShell;
        public LineFx lightning;
        public LineFx infernoBeam;
        [Header("Colores de señal (UXS-001.2, UXS-002.4)")]
        public Color valid = Color.white;
        public Color invalid = new Color(1f, 0.42f, 0.42f);
        public Color edge = new Color(0.106f, 0.122f, 0.149f);
        public Color accent = new Color(0.27f, 0.816f, 0.769f);
        public Color gold = new Color(0.949f, 0.757f, 0.306f);
        public Color fire = new Color(1f, 0.541f, 0.122f);
        public Color immune = Color.white;
        public Color life = new Color(1f, 0.353f, 0.373f);
        public Color text = new Color(0.949f, 0.949f, 0.949f);
        public Color bomb = new Color(0.761f, 0.231f, 0.231f);
        public Color orb = new Color(0.71f, 0.482f, 1f);
        public Color lightningColor = new Color(1f, 0.965f, 0.659f);
        public Color infernoColor = new Color(0.878f, 0.271f, 0.482f);
    }
}
