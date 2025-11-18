using UnityEngine;
using UnityEngine.UI;

public class TowerSelectUI : MonoBehaviour
{
    public TowerPlacer placer;       // arrastrá el TowerPlacer
    public TowerId towerId = TowerId.Basic;
    public Image selectedGlow;       // hijo "SelectedGlow" (Image)

    static TowerSelectUI current;

    void OnEnable()
    {
        if (placer != null)
            placer.OnSelectionCleared += Deselect;
    }

    void OnDisable()
    {
        if (placer != null)
            placer.OnSelectionCleared -= Deselect;
    }

    public void SelectThisTower()
    {
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
