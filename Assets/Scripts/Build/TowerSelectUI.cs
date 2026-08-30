using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TowerSelectUI : MonoBehaviour
{
    public TowerPlacer placer;
    public TowerId towerId;
    public TowerData towerData;
    public Image selectedGlow;

    static TowerSelectUI current;

    void Update()
    {
        if (current != this) return;
        AutoBind();
        if (placer != null && !placer.HasSelection)
            Deselect();
    }

    void AutoBind()
    {
        if (placer == null)
            placer = FindFirstObjectByType<TowerPlacer>(FindObjectsInactive.Include);
    }

    public void SelectThisTower()
    {
        AutoBind();
        if (!placer) return;

        placer.SelectTower(towerId);

        if (current != null && current != this && current.selectedGlow != null)
            current.selectedGlow.enabled = false;

        if (selectedGlow != null)
            selectedGlow.enabled = true;

        current = this;
    }

    void Deselect()
    {
        if (selectedGlow != null)
            selectedGlow.enabled = false;

        if (current == this)
            current = null;

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }
}
