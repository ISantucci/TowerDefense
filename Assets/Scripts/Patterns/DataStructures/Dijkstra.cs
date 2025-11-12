using System.Collections.Generic;

public static class Dijkstra
{
    // Grafo por listas de adyacencia: adj[u] = lista de (v, costo)
    public static List<int> ShortestPath(int n, List<(int to, float w)>[] adj, int src, int dst)
    {
        var dist = new float[n];
        var prev = new int[n];
        var used = new bool[n];

        for (int i = 0; i < n; i++) { dist[i] = float.PositiveInfinity; prev[i] = -1; }
        dist[src] = 0f;

        for (int it = 0; it < n; it++)
        {
            int u = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                if (!used[i] && dist[i] < best) { best = dist[i]; u = i; }
            }
            if (u == -1) break;
            if (u == dst) break; // ya llegamos
            used[u] = true;

            var list = adj[u];
            for (int k = 0; k < list.Count; k++)
            {
                int v = list[k].to;
                float w = list[k].w;
                float nd = dist[u] + w;
                if (nd < dist[v]) { dist[v] = nd; prev[v] = u; }
            }
        }

        // reconstrucción de camino
        var path = new List<int>();
        int cur = dst;
        while (cur != -1) { path.Add(cur); cur = prev[cur]; }
        path.Reverse();
        // si el destino no es alcanzable, path no terminará en dst
        if (path.Count == 0 || path[path.Count - 1] != dst) path.Clear();
        return path;
    }
}
