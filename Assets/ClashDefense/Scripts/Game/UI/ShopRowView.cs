using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashDefense.Game
{
    /// <summary>Una mejora de la tienda con su botón de compra (UXS-002.3). Va en el prefab FilaMejora.</summary>
    [AddComponentMenu("Clash Defense/UI/Fila de mejora")]
    public sealed class ShopRowView : MonoBehaviour
    {
        [SerializeField] internal TextMeshProUGUI text;
        [SerializeField] internal Button buy;
        [SerializeField] internal TextMeshProUGUI buyLabel;

        public string ItemId { get; set; }
        public RectTransform Rect => (RectTransform)transform;
    }
}
