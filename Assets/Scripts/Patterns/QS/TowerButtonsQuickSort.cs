using UnityEngine;
using UnityEngine.UI;

public class TowerButtonsQuickSort : MonoBehaviour
{
    [Header("Tower Buttons")]
    public TowerSelectUI[] towerButtons;

    [Header("Slots")]
    public Transform[] slots; 

    public void SortButtonsByCost()
    {
        if (towerButtons == null || towerButtons.Length == 0) return;
        if (slots.Length < towerButtons.Length) return;

        ButtonCostItem[] items = new ButtonCostItem[towerButtons.Length];

        for (int i = 0; i < towerButtons.Length; i++)
        {
            items[i] = new ButtonCostItem
            {
                button = towerButtons[i].GetComponent<Button>(),
                cost = towerButtons[i].towerData.cost
            };
        }

        QS.QuickSort(items, 0, items.Length - 1);

        for (int i = 0; i < items.Length; i++)
        {
            items[i].button.transform.SetParent(slots[i], false);
            items[i].button.transform.localPosition = Vector3.zero;
        }
    }
    void Start()
    {
        SortButtonsByCost();
    }
}
