using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashDefense.Game
{
    /// <summary>Tarjeta de un nivel en el mapa del mundo (UXS-002.2): nombre, estrellas, estado y premio. Va en el prefab TarjetaNivel.</summary>
    [AddComponentMenu("Clash Defense/UI/Tarjeta de nivel")]
    public sealed class LevelCardView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal GameObject selectionFrame;
        [SerializeField] internal TextMeshProUGUI nameText, stateText, rewardText;
        [SerializeField] internal Image[] stars = new Image[3];

        public string LevelId { get; set; }
        public RectTransform Rect => (RectTransform)transform;
    }
}
