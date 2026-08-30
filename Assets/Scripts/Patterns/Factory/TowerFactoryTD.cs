using System;
using System.Collections.Generic;
using UnityEngine;

// IMPORTANTE: los valores se serializan por número. Archer y Bomber quedan primero;
// las entradas nuevas se agregan SIEMPRE al final y nunca se reordenan.
public enum TowerId
{
    Archer = 0,
    Bomber = 1,

    // --- Clash of Clans: Home Village ---
    CoC_Home_Cannon,
    CoC_Home_ArcherTower,
    CoC_Home_Mortar,
    CoC_Home_AirDefense,
    CoC_Home_WizardTower,
    CoC_Home_AirSweeper,
    CoC_Home_HiddenTesla,
    CoC_Home_BombTower,
    CoC_Home_XBow,
    CoC_Home_InfernoTower,
    CoC_Home_EagleArtillery,
    CoC_Home_Scattershot,
    CoC_Home_BuildersHut,
    CoC_Home_SpellTower,
    CoC_Home_Monolith,
    CoC_Home_MultiArcherTower,
    CoC_Home_RicochetCannon,
    CoC_Home_MultiGearTower,
    CoC_Home_Firespitter,
    CoC_Home_GigaTesla,
    CoC_Home_GigaInferno,
    CoC_Home_Bomb,
    CoC_Home_SpringTrap,
    CoC_Home_AirBomb,
    CoC_Home_GiantBomb,
    CoC_Home_SeekingAirMine,
    CoC_Home_SkeletonTrap,
    CoC_Home_TornadoTrap,
    CoC_Home_GigaBomb,

    // --- Clash of Clans: Builder Base ---
    CoC_Builder_Cannon,
    CoC_Builder_DoubleCannon,
    CoC_Builder_ArcherTower,
    CoC_Builder_HiddenTesla,
    CoC_Builder_Firecrackers,
    CoC_Builder_Mortar,
    CoC_Builder_Crusher,
    CoC_Builder_GuardPost,
    CoC_Builder_AirBombs,
    CoC_Builder_MultiMortar,
    CoC_Builder_Roaster,
    CoC_Builder_GiantCannon,
    CoC_Builder_MegaTesla,
    CoC_Builder_LavaLauncher,
    CoC_Builder_XBow,
    CoC_Builder_PushTrap,
    CoC_Builder_SpringTrap,
    CoC_Builder_Mine,
    CoC_Builder_MegaMine,

    // --- Clash of Clans: Clan Capital ---
    CoC_Capital_Cannon,
    CoC_Capital_ArcherTower,
    CoC_Capital_AirDefense,
    CoC_Capital_RapidCannon,
    CoC_Capital_Mortar,
    CoC_Capital_BombTower,
    CoC_Capital_BlastBow,
    CoC_Capital_SpearThrower,
    CoC_Capital_SuperWizardTower,
    CoC_Capital_MultiCannon,
    CoC_Capital_SuperDragonPost,
    CoC_Capital_RocketArtillery,
    CoC_Capital_HiddenMegaTesla,
    CoC_Capital_GiantCannon,
    CoC_Capital_MultiMortar,
    CoC_Capital_InfernoTower,
    CoC_Capital_Crusher,
    CoC_Capital_AirBlaster,
    CoC_Capital_MiniMinionHive,
    CoC_Capital_SuperGiantPost,
    CoC_Capital_Reflector,
    CoC_Capital_GoblinThrower,
    CoC_Capital_FlameSpinner,
    CoC_Capital_Mine,
    CoC_Capital_MegaMine,
    CoC_Capital_LogTrap,
    CoC_Capital_ZapTrap,
    CoC_Capital_SpearTrap,

    // --- Clash Royale ---
    CR_Cannon,
    CR_Tesla,
    CR_InfernoTower,
    CR_BombTower,
    CR_Mortar,
    CR_XBow,
    CR_GoblinCage,
    CR_GoblinDrill,
    CR_Tombstone,
    CR_Furnace,
    CR_GoblinHut,
    CR_BarbarianHut,
    CR_ElixirCollector,
    CR_PrincessTower,
    CR_KingTower,
    CR_TowerPrincess,
    CR_Cannoneer,
    CR_DaggerDuchess,
    CR_RoyalChef,

    // --- Boom Beach ---
    BoomBeach_SniperTower,
    BoomBeach_MachineGun,
    BoomBeach_Mortar,
    BoomBeach_Cannon,
    BoomBeach_Flamethrower,
    BoomBeach_BoomCannon,
    BoomBeach_RocketLauncher,
    BoomBeach_ShockLauncher,
    BoomBeach_LazorBeam,
    BoomBeach_DoomCannon,
    BoomBeach_ShockBlaster,
    BoomBeach_SIMO,
    BoomBeach_HotPot,
    BoomBeach_Grappler,
    BoomBeach_Microwavr,
    BoomBeach_SkyShield,
    BoomBeach_FlotsamCannon,
    BoomBeach_BoomSurprise,
    BoomBeach_DamageAmplifier,
    BoomBeach_ShieldGenerator,
    BoomBeach_Mine,
    BoomBeach_BoomMine,
    BoomBeach_ShockMine,
}

public class TowerFactoryTD : MonoBehaviour
{
    const string ResourcesFolder = "Towers";

    [Header("Catálogo de Torres (Type Object)")]
    public List<TowerData> towerCatalog = new();

    /// <summary>Catálogo completo (escena + Resources/Towers), sólo lectura.</summary>
    public IReadOnlyList<TowerData> Catalog => towerCatalog;

    void Awake()
    {
        MergeResourcesIntoCatalog();
    }

    /// <summary>Suma a towerCatalog los TowerData de Resources/Towers (recursivo), sin nulos ni ids duplicados.</summary>
    void MergeResourcesIntoCatalog()
    {
        if (towerCatalog == null) towerCatalog = new List<TowerData>();

        var loaded = Resources.LoadAll<TowerData>(ResourcesFolder);
        if (loaded == null) return;

        foreach (var d in loaded)
        {
            if (d == null) continue;
            if (GetData(d.id) != null) continue;   // ya hay una entrada con ese id (escena gana)
            towerCatalog.Add(d);
        }
    }

    public TowerData GetData(TowerId id)
    {
        if (towerCatalog == null) return null;

        foreach (var d in towerCatalog)
        {
            if (d != null && d.id == id)
                return d;
        }

        return null;
    }

    public IEnumerable<TowerData> GetBySource(DefenseSource s)
    {
        if (towerCatalog == null) yield break;

        foreach (var d in towerCatalog)
        {
            if (d != null && d.source == s)
                yield return d;
        }
    }

    public int GetCost(TowerId id)
    {
        var d = GetData(id);
        return d != null ? d.cost : 0;
    }

    public Tower Create(TowerId id, Vector3 position, Quaternion rotation)
    {
        var data = GetData(id);
        if (data == null)
        {
            return null;
        }

        if (data.prefab == null)
        {
            return null;
        }

        var tower = Instantiate(data.prefab, position, rotation);
        if (tower == null)
        {
            return null;
        }

        // Aplico datos Type Object
        tower.ApplyData(data);
        tower.name = "Tower_" + data.DisplayName;

        // Apariencia por familia / tipo de ataque (mismo prefab, distinto color)
        TowerVisual.Apply(tower, data);

        // Encolar evento
        EventQueueManager.Enqueue(
            new GameplayEvent(
                GameplayEventType.TowerBuilt,
                (int)id,
                data.cost
            )
        );

        CombatEvents.RaiseTowerPlaced(tower);
        return tower;
    }
}
