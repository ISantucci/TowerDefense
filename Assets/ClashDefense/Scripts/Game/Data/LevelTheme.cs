using UnityEngine;

namespace ClashDefense.Game
{
    /// <summary>
    /// Aspecto de un continente (UXS-002.2: cada continente con rasgos propios): los materiales del terreno, lo que queda fuera
    /// del área, el camino y las rocas. Los niveles lo usan para dibujarse y el mapa del mundo para la franja del continente.
    /// Cambiar el color de un material cambia todos los niveles del continente.
    /// </summary>
    [CreateAssetMenu(menuName = "Clash Defense/Datos/Tema de continente", fileName = "Tema_", order = 30)]
    public sealed class LevelTheme : ScriptableObject
    {
        [Tooltip("Nombre corto que viaja en los datos del nivel (\"praderas\", \"desfiladero\"). Vacío = gris del P0.")]
        public string id;
        public Material terrain;
        [Tooltip("Lo que rodea al área construible.")]
        public Material outside;
        public Material path;
        public Material rock;
        [Tooltip("Cara clara de las rocas.")]
        public Material rockLight;
        [Header("Base y entradas")]
        public Material baseBlock;
        public Material baseBattlement;
        [Tooltip("Arcos de entrada y puertas de la base.")]
        public Material entrance;

        /// <summary>Color de la franja del continente en el mapa del mundo.</summary>
        public Color StripeColor => terrain != null ? terrain.color : Color.gray;
    }
}
