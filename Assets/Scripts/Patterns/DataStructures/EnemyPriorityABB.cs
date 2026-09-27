using UnityEngine;

public class EnemyPriorityABB : MonoBehaviour
{
    public static EnemyPriorityABB Instance { get; private set; }

    private class Node
    {
        public EnemyProgress prog;
        public Node left, right;
        public Node(EnemyProgress p) { prog = p; }
    }

    Node root;
    EnemyProgress currentClosest;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public EnemyTD GetMostAdvancedInRange(Vector3 origin, float range)
    {
        return SearchInRange(root, origin, range * range);
    }

    EnemyTD SearchInRange(Node node, Vector3 origin, float rangeSq)
    {
        if (node == null) return null;

        var result = SearchInRange(node.right, origin, rangeSq);
        if (result != null) return result;

        if (node.prog != null && node.prog.gameObject != null)
        {
            float distSq = (node.prog.transform.position - origin).sqrMagnitude;
            if (distSq <= rangeSq)
                return node.prog.GetComponent<EnemyTD>();
        }

        return SearchInRange(node.left, origin, rangeSq);
    }


    // Llamar al inicio de cada wave
    public void Clear()
    {
        // restaurar color si hab�a uno
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

        // engancho a en el m�s chico de b
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

    // === Obtener el m�s avanzado (mayor progress) ===
    EnemyProgress GetMostAdvanced()
    {
        if (root == null) return null;

        Node node = root;
        while (node.right != null) node = node.right;

        // por seguridad, limpiar si el enemigo ya no existe
        if (node.prog == null || node.prog.gameObject == null)
            return null;

        return node.prog;
    }

    // === Manejo de color + logs ===
    void UpdateVisuals()
    {
        var newClosest = GetMostAdvanced();

        if (newClosest != currentClosest)
        {
            if (currentClosest != null)
                SetColor(currentClosest, Color.white);

            if (newClosest != null)
            {
                SetColor(newClosest, Color.red);
            }

            currentClosest = newClosest;
        }
    }

    void SetColor(EnemyProgress p, Color c)
    {
        if (p == null) return;
        var rend = p.GetComponentInChildren<Renderer>();
        if (rend != null && rend.material != null)
            rend.material.color = c;
    }
}
