using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashDefense.Game
{
    /// <summary>Columna de un continente en el mapa del mundo (UXS-002.2). Va en el prefab ColumnaContinente.</summary>
    [AddComponentMenu("Clash Defense/UI/Columna de continente")]
    public sealed class ContinentColumnView : MonoBehaviour
    {
        [SerializeField] internal Image panel, stripe;
        [SerializeField] internal TextMeshProUGUI nameText, tandaText;
        [Tooltip("Donde van las tarjetas de nivel.")]
        [SerializeField] internal RectTransform cards;

        public RectTransform Rect => (RectTransform)transform;
    }
}
