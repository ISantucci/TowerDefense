using UnityEngine;
using UnityEngine.UI;

public class TowerSelectUI : MonoBehaviour
{
    public TowerPlacer placer;
    public TowerId towerId;
    public TowerData towerData;
    public Image selectedGlow;

    static TowerSelectUI current;

    void OnEnable()
    {
        if (placer != null)
            placer.OnSelectionCleared += Deselect;
        AutoBind();
    }

    void OnDisable()
    {
        if (placer != null)
            placer.OnSelectionCleared -= Deselect;
    }
    
    void AutoBind()
    {
        if (placer == null)
            placer = FindObjectOfType<TowerPlacer>(true);
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
    }
}

