using System.Collections.Generic;
using UnityEngine;

public class GridMap : MonoBehaviour
{
    [Header("Grid dimensions")]
    public int   width    = 20;
    public int   height   = 20;
    public float cellSize = 1f;

    [Header("World anchor")]
    public Vector3 origin = Vector3.zero;

    [Header("Data")]
    [SerializeField] GridNode[] nodes;

    // ── Public read ─────────────────────────────────────────────────────────

    public int NodeCount => nodes != null ? nodes.Length : 0;

    public GridNode GetNode(int x, int y)
    {
        if (!InBounds(x, y)) return null;
        return nodes[y * width + x];
    }

    public GridNode GetNode(Vector2Int pos) => GetNode(pos.x, pos.y);

    public Vector3 GridToWorld(Vector2Int pos)
    {
        return origin + new Vector3(pos.x * cellSize + cellSize * 0.5f,
                                    0f,
                                    pos.y * cellSize + cellSize * 0.5f);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        Vector3 local = worldPos - origin;
        return new Vector2Int(Mathf.FloorToInt(local.x / cellSize),
                              Mathf.FloorToInt(local.z / cellSize));
    }

    public bool IsWalkable(GridNode node, int currentRound)
    {
        if (node == null) return false;
        return node.walkable && node.unlockRound <= currentRound;
    }

    public List<GridNode> GetNeighbors(GridNode node, int currentRound)
    {
        var result = new List<GridNode>(4);
        if (node == null) return result;

        int x = node.gridPos.x;
        int y = node.gridPos.y;

        TryAddNeighbor(x + 1, y,     currentRound, result); // este
        TryAddNeighbor(x - 1, y,     currentRound, result); // oeste
        TryAddNeighbor(x,     y + 1, currentRound, result); // norte
        TryAddNeighbor(x,     y - 1, currentRound, result); // sur

        return result;
    }

    // ── Generation ──────────────────────────────────────────────────────────

    [ContextMenu("Generate Grid")]
    public void GenerateGrid()
    {
        nodes = new GridNode[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var gp = new Vector2Int(x, y);
                nodes[y * width + x] = new GridNode(gp, GridToWorld(gp));
            }
        }
        Debug.Log($"[GridMap] Grilla generada: {width}×{height} = {nodes.Length} nodos.", this);
    }

    // ── Internal helpers ────────────────────────────────────────────────────

    bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    void TryAddNeighbor(int x, int y, int currentRound, List<GridNode> result)
    {
        var n = GetNode(x, y);
        if (n != null && IsWalkable(n, currentRound))
            result.Add(n);
    }

    // ── Gizmos ──────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (nodes == null || nodes.Length == 0)
        {
            DrawGridOutline();
            return;
        }

        foreach (var node in nodes)
        {
            if (node == null) continue;
            Gizmos.color = GizmoColor(node);
            Gizmos.DrawCube(node.worldPos, Vector3.one * cellSize * 0.85f);
        }
    }

    void DrawGridOutline()
    {
        Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        Vector3 size = new Vector3(width * cellSize, 0.05f, height * cellSize);
        Vector3 center = origin + size * 0.5f;
        Gizmos.DrawWireCube(center, size);
    }

    Color GizmoColor(GridNode node)
    {
        if (!node.walkable)
            return new Color(0.8f, 0.1f, 0.1f, 0.7f);          // rojo = bloqueado

        if (node.unlockRound > 0)
            return new Color(1f, 0.55f, 0f, 0.7f);              // naranja = bloqueado por ronda

        if (node.movementCost > 2f)
            return new Color(0.9f, 0.85f, 0.1f, 0.7f);          // amarillo = costo alto

        if (node.movementCost > 1f)
            return new Color(0.4f, 0.75f, 0.3f, 0.7f);          // verde claro = costo medio

        return new Color(0.2f, 0.55f, 0.2f, 0.55f);             // verde = normal
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        width    = Mathf.Max(1, width);
        height   = Mathf.Max(1, height);
        cellSize = Mathf.Max(0.1f, cellSize);

        if (nodes != null)
        {
            foreach (var node in nodes)
            {
                if (node == null) continue;
                if (node.movementCost <= 0f)
                {
                    Debug.LogWarning($"[GridMap] Nodo {node.gridPos} tiene movementCost={node.movementCost:F2}. Será tratado como 1.", this);
                }
            }
        }
    }
#endif
}
