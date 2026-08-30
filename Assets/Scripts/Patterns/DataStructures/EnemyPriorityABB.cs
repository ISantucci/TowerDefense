using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ABB (BST) de enemigos ordenado por progressValue. El más avanzado es el nodo más a la derecha.
/// Las consultas de targeting recorren en in-order inverso (más avanzado primero) y filtran por
/// distancia, rango mínimo y capa (tierra / aire).
/// </summary>
public class EnemyPriorityABB : MonoBehaviour
{
    public static EnemyPriorityABB Instance { get; private set; }

    private class Node
    {
        public EnemyProgress prog;
        public EnemyTD enemy;
        public Node left, right;

        public Node(EnemyProgress p)
        {
            prog = p;
            enemy = p != null ? p.GetComponent<EnemyTD>() : null;
        }
    }

    Node root;
    EnemyProgress currentClosest;

    // Pila reutilizable para el recorrido iterativo (sin riesgo de recursión profunda).
    readonly Stack<Node> traversal = new Stack<Node>(64);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ================== TARGETING ==================

    /// <summary>Enemigo más avanzado dentro de range (cualquier capa, sin rango mínimo).</summary>
    public EnemyTD GetMostAdvancedInRange(Vector3 origin, float range)
    {
        return GetMostAdvancedInRange(origin, range, 0f, TargetLayer.Both);
    }

    /// <summary>
    /// Enemigo más avanzado dentro de [minRange, range] cuya capa coincide con layer.
    /// Recorrido in-order inverso: devuelve el PRIMERO que cumple.
    /// </summary>
    public EnemyTD GetMostAdvancedInRange(Vector3 origin, float range, float minRange, TargetLayer layer)
    {
        traversal.Clear();
        Node node = root;

        while (node != null || traversal.Count > 0)
        {
            while (node != null)
            {
                traversal.Push(node);
                node = node.right;
            }

            node = traversal.Pop();

            if (IsValidTarget(node, origin, range, minRange, layer))
                return node.enemy;

            node = node.left;
        }

        return null;
    }

    /// <summary>
    /// Llena results (limpiándola primero) con los enemigos válidos, del más avanzado al menos,
    /// hasta max (max &lt;= 0 = sin límite). Devuelve la cantidad agregada.
    /// </summary>
    public int GetTargetsInRange(Vector3 origin, float range, float minRange, TargetLayer layer, int max, List<EnemyTD> results)
    {
        if (results == null) return 0;
        results.Clear();

        if (max <= 0) max = int.MaxValue;

        traversal.Clear();
        Node node = root;
        int count = 0;

        while ((node != null || traversal.Count > 0) && count < max)
        {
            while (node != null)
            {
                traversal.Push(node);
                node = node.right;
            }

            node = traversal.Pop();

            if (IsValidTarget(node, origin, range, minRange, layer))
            {
                results.Add(node.enemy);
                count++;
            }

            node = node.left;
        }

        return count;
    }

    bool IsValidTarget(Node node, Vector3 origin, float range, float minRange, TargetLayer layer)
    {
        if (node == null) return false;

        // Comparación con null de Unity: cubre objetos destruidos.
        if (node.prog == null) return false;
        if (node.enemy == null)
        {
            node.enemy = node.prog.GetComponent<EnemyTD>();
            if (node.enemy == null) return false;
        }

        if (!MatchesLayer(node.enemy, layer)) return false;

        float d = Vector3.Distance(origin, node.prog.transform.position);
        if (d > range) return false;
        if (minRange > 0f && d < minRange) return false;

        return true;
    }

    static bool MatchesLayer(EnemyTD enemy, TargetLayer layer)
    {
        if (enemy.IsFlying)
            return (layer & TargetLayer.Air) != 0;
        return (layer & TargetLayer.Ground) != 0;
    }

    // ================== MANTENIMIENTO ==================

    // Llamar al inicio de cada wave
    public void Clear()
    {
        // restaurar color si había uno
        if (currentClosest != null)
            SetColor(currentClosest, Color.white);

        root = null;
        currentClosest = null;
    }

    // === Insertar ===
    public void Insert(EnemyProgress p)
    {
        if (p == null) return;
        root = InsertRec(root, p);
        UpdateVisuals();
    }

    Node InsertRec(Node node, EnemyProgress p)
    {
        if (node == null) return new Node(p);

        if (p.progressValue < node.prog.progressValue)
            node.left = InsertRec(node.left, p);
        else
            node.right = InsertRec(node.right, p);

        return node;
    }

    // === Eliminar ===
    public void Remove(EnemyProgress p)
    {
        if (p == null) return;
        root = RemoveRec(root, p);
        UpdateVisuals();
    }

    Node RemoveRec(Node node, EnemyProgress p)
    {
        if (node == null) return null;

        if (node.prog == p)
            return Merge(node.left, node.right);

        if (p.progressValue < node.prog.progressValue)
            node.left = RemoveRec(node.left, p);
        else
            node.right = RemoveRec(node.right, p);

        return node;
    }

    Node Merge(Node a, Node b)
    {
        if (a == null) return b;
        if (b == null) return a;

        // engancho a en el más chico de b
        Node minRight = b;
        while (minRight.left != null) minRight = minRight.left;
        minRight.left = a;
        return b;
    }

    // === Actualizar (cuando cambia el progress) ===
    public void UpdateProgress(EnemyProgress p)
    {
        // simple: saco y vuelvo a meter con el nuevo valor
        Remove(p);
        Insert(p);
    }

    // === Obtener el más avanzado (mayor progress) ===
    EnemyProgress GetMostAdvanced()
    {
        if (root == null) return null;

        Node node = root;
        while (node.right != null) node = node.right;

        // por seguridad, limpiar si el enemigo ya no existe
        if (node.prog == null)
            return null;

        return node.prog;
    }

    // === Manejo de color ===
    void UpdateVisuals()
    {
        var newClosest = GetMostAdvanced();

        if (newClosest != currentClosest)
        {
            if (currentClosest != null)
                SetColor(currentClosest, Color.white);

            if (newClosest != null)
                SetColor(newClosest, Color.red);

            currentClosest = newClosest;
        }
    }

    void SetColor(EnemyProgress p, Color c)
    {
        // El "líder" (más avanzado) se resalta a través de EnemyVisual para no pisar el tinte del enemigo.
        if (p == null) return;
        EnemyVisual.SetLeader(p.gameObject, c == Color.red);
    }
}
