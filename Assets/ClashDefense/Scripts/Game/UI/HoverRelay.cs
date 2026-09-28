using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ClashDefense.Game
{
    /// <summary>Avisa cuando el mouse entra y sale de un elemento (vista previa de la mejora, ayuda de la tarjeta; UXS-001.3, UXS-002.4).</summary>
    [AddComponentMenu("Clash Defense/UI/Aviso de mouse encima")]
    public sealed class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Enter, Exit;
        public void OnPointerEnter(PointerEventData e) => Enter?.Invoke();
        public void OnPointerExit(PointerEventData e) => Exit?.Invoke();
        void OnDisable() => Exit?.Invoke();
    }
}
