using UnityEngine;

[System.Serializable]
public class GridNode
{
    public Vector2Int gridPos;
    public Vector3    worldPos;
    public bool       walkable       = true;
    public float      movementCost   = 1f;
    public int        unlockRound    = 0;

    public GridNode(Vector2Int gridPos, Vector3 worldPos)
    {
        this.gridPos  = gridPos;
        this.worldPos = worldPos;
    }
}
