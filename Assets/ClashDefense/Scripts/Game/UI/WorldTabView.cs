using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashDefense.Game
{
    /// <summary>Pestaña de un mundo en el mapa (UXS-002.2). Va en el prefab PestanaMundo.</summary>
    [AddComponentMenu("Clash Defense/UI/Pestaña de mundo")]
    public sealed class WorldTabView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal TextMeshProUGUI label;
        [SerializeField] internal GameObject selectionFrame;

        public void Bind(string text, bool open, bool selected)
        {
            label.text = open ? text : $"{text} · bloqueado";
            label.color = open ? Palette.Texto : Palette.Bloqueo;
            if (selectionFrame.activeSelf != selected) selectionFrame.SetActive(selected);
        }
    }
}
