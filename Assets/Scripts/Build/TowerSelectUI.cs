// Assets/Scripts/Build/TowerSelectUI.cs
using UnityEngine;
using UnityEngine.UI;

public class TowerSelectUI : MonoBehaviour
{
    public TowerPlacer placer;          // arrastrá _Managers/TowerPlacer_GO
    public TowerId towerId = TowerId.Basic;
    public int cost = 50;
    public Image selectedGlow;          // arrastrá el hijo "SelectedGlow"

    // estado simple de selección
    static TowerSelectUI current;

    public void SelectThisTower()
    {
        if (placer == null) { Debug.LogWarning("[TowerSelectUI] Placer no asignado"); return; }

        placer.SelectTower(towerId, cost);
        Debug.Log($"[TowerSelectUI] Seleccionado {towerId} (${cost})");

        // Apago el glow del anterior, enciendo el mío
        if (current != null && current != this && current.selectedGlow != null)
            current.selectedGlow.enabled = false;

        if (selectedGlow != null)
            selectedGlow.enabled = true;

        current = this;
    }
}
