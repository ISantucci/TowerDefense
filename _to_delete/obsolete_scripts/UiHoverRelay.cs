using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Reenvía enter/exit del puntero a callbacks (tooltips, resaltado). Sin estado propio.</summary>
public class UiHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action onEnter;
    public System.Action onExit;

    bool hovering;

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovering = true;
        if (onEnter != null) onEnter();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        if (onExit != null) onExit();
    }

    void OnDisable()
    {
        if (!hovering) return;
        hovering = false;
        if (onExit != null) onExit();
    }
}
