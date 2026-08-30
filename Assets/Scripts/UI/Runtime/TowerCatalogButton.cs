using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Una carta de la barra de torres (Slot_N del prefab LevelHUD). Muestra un TowerData.</summary>
public class TowerCatalogButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Piezas (cableadas en el prefab)")]
    public Button button;
    public Image background;
    public Image icon;
    public Text nameText;
    public Text costText;
    public Text hotkeyText;
    public GameObject selectedFrame;
    public CanvasGroup group;

    public TowerData Data { get; private set; }
    public int Index { get; private set; }

    TowerCatalogUI owner;

    static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
    static readonly Color Danger = new Color(1f, 0.4f, 0.35f);

    public void Bind(TowerCatalogUI catalog, TowerData data, int index)
    {
        owner = catalog;
        Data = data;
        Index = index;
        gameObject.SetActive(data != null);
        if (data == null) return;

        if (nameText != null) nameText.text = data.DisplayName;
        if (costText != null) costText.text = data.cost + " oro";
        if (hotkeyText != null) hotkeyText.text = (index + 1).ToString();
        if (icon != null)
        {
            var sprite = TowerIconFactory.Get(data);
            if (sprite != null) icon.sprite = sprite;
            icon.color = Color.white;
        }
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }
        SetSelected(false);
    }

    void OnClick()
    {
        if (owner != null) owner.OnCardClicked(this);
    }

    public void SetSelected(bool on)
    {
        if (selectedFrame != null) selectedFrame.SetActive(on);
    }

    public void SetAffordable(bool ok)
    {
        if (group != null) group.alpha = ok ? 1f : 0.45f;
        if (costText != null) costText.color = ok ? Gold : Danger;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner != null) owner.ShowTooltip(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null) owner.HideTooltip(this);
    }
}
