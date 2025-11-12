using System.Collections.Generic;
using UnityEngine;

public class GraphNode
{
    public Transform waypoint;
    public readonly List<GraphEdge> edges = new();

    public GraphNode(Transform wp) { waypoint = wp; }
}

public class GraphEdge
{
    public GraphNode target;
    public float cost;

    public GraphEdge(GraphNode target, float cost)
    {
        this.target = target;
        this.cost = cost;
    }
}
