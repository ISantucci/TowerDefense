// Assets/Scripts/Patterns/DataStructure/EnemyGraphPath.cs
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class EnemyGraphPath : MonoBehaviour
{
    public WaypointsPath path;

    readonly List<Transform> computedPath = new();
    List<(int to, float w)>[] adj;
    bool graphBuilt;

    void OnEnable() { TryBuildAndCompute(); }
    void Start() { TryBuildAndCompute(); }

    void TryBuildAndCompute()
    {
        if (path == null || path.points == null || path.points.Length == 0) return;
        BuildGraphFromWaypoints();
        ComputeDijkstraLinear(0, path.points.Length - 1);
    }

    void BuildGraphFromWaypoints()
    {
        int n = path.points.Length;
        adj = new List<(int to, float w)>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<(int to, float w)>();

        for (int i = 0; i < n - 1; i++)
        {
            float w = Vector3.Distance(path.points[i].position, path.points[i + 1].position);
            adj[i].Add((i + 1, w));
        }
        graphBuilt = true;
    }

    void ComputeDijkstraLinear(int src, int dst)
    {
        computedPath.Clear();
        if (!graphBuilt) return;

        int n = path.points.Length;
        var idxPath = Dijkstra.ShortestPath(n, adj, src, dst);
        foreach (var idx in idxPath) computedPath.Add(path.points[idx]);
    }

    public IReadOnlyList<Transform> ComputeAndGetPath()
    {
        TryBuildAndCompute();
        return computedPath;
    }

    void OnDrawGizmos()
    {
        if (path == null || path.points == null) return;

        // Cyan: waypoints
        Gizmos.color = Color.cyan;
        for (int i = 0; i < path.points.Length - 1; i++)
            if (path.points[i] && path.points[i + 1])
                Gizmos.DrawLine(path.points[i].position, path.points[i + 1].position);

        // Amarillo: ruta elegida por Dijkstra
        Gizmos.color = Color.yellow;
        for (int i = 0; i < computedPath.Count - 1; i++)
            if (computedPath[i] && computedPath[i + 1])
                Gizmos.DrawLine(computedPath[i].position, computedPath[i + 1].position);
    }
}
