using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashDefense.Game
{
    /// <summary>Una opción de la mejora de tanda (Doc 02 §8, UXS-002.3). Va en el prefab OpcionTanda.</summary>
    [AddComponentMenu("Clash Defense/UI/Opción de tanda")]
    public sealed class TandaOptionView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image stripe;
        [SerializeField] internal TextMeshProUGUI keyText, nameText, effectText;

        public string TowerId { get; set; }
        public RectTransform Rect => (RectTransform)transform;
    }
}
