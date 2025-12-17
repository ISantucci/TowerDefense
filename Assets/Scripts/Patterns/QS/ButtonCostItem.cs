using UnityEngine;
using UnityEngine.UI;

public class ButtonCostItem : System.IComparable<ButtonCostItem>
{
    public Button button;
    public int cost;

    public int CompareTo(ButtonCostItem other)
    {
        return cost.CompareTo(other.cost);
    }
}
