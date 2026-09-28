using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashDefense.Game
{
    /// <summary>Tarjeta de una torre en la tienda (UXS-002.3). Va en el prefab TarjetaTienda.</summary>
    [AddComponentMenu("Clash Defense/UI/Tarjeta de tienda")]
    public sealed class ShopCardView : MonoBehaviour
    {
        [SerializeField] internal Image panel, stripe;
        [SerializeField] internal TextMeshProUGUI nameText, statusText, tandaText, futureText;
        [Tooltip("Donde van las filas de mejoras.")]
        [SerializeField] internal RectTransform rows;

        public RectTransform Rect => (RectTransform)transform;
    }
}
