using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BuildSnapshot
{
    [Serializable]
    public class TowerData
    {
        public TowerId towerId;
        public Vector3 position;
        public Quaternion rotation;
    }

    public List<TowerData> towers = new();
    public int money;
    public int lives;
    public int score;
}
