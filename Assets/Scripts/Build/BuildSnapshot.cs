using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BuildSnapshot
{
    public int money;
    public int lives;
    public int score;

    [Serializable]
    public struct TowerInfo
    {
        public TowerId id;
        public Vector3 pos;
        public Quaternion rot;
    }

    public List<TowerInfo> towers = new List<TowerInfo>();

    public static BuildSnapshot Capture()
    {
        var snap = new BuildSnapshot();

        var all = UnityEngine.Object.FindObjectsByType<Tower>(FindObjectsSortMode.None);
        foreach (var t in all)
        {
            snap.towers.Add(new TowerInfo
            {
                id = t.towerType,
                pos = t.transform.position,
                rot = t.transform.rotation
            });
        }

        if (GameManager.I != null)
        {
            snap.money = GameManager.I.Money;
            snap.lives = GameManager.I.Lives;
            snap.score = GameManager.I.Score;
        }

        return snap;
    }

    public void Restore(TowerFactoryTD factory)
    {
        if (GameManager.I != null)
            GameManager.I.SetMoneyLivesScore(money, lives, score);

        if (factory == null) return;

        var all = UnityEngine.Object.FindObjectsByType<Tower>(FindObjectsSortMode.None);
        foreach (var t in all)
            UnityEngine.Object.Destroy(t.gameObject);

        foreach (var info in towers)
            factory.Create(info.id, info.pos, info.rot);
    }
}
