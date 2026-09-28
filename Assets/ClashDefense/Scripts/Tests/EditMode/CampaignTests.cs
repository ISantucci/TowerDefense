using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClashDefense.Core;
using ClashDefense.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ClashDefense.Tests
{
    /// <summary>
    /// El Mundo 1 tal como lo usa el juego: desde sus assets (TL-003: la campaña, el balance y los niveles son
    /// ScriptableObjects; los JSON son una exportación para el simulador). Los datos tienen que validar (GDS-002.0, RQ-002.3).
    /// </summary>
    public class CampaignTests
    {
        internal static GameConfig Cfg() => AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/ClashDefense/Data/Juego_Mundo1.asset");
        static BalanceData B() => Cfg().campaign.balance.ToCore();
        static CampaignData C() => Cfg().campaign.ToCore();
        static LevelData L(int n) => Cfg().campaign.FindLevel($"m1_n{n}").ToCore();

        [Test] public void ElMundo1ValidaConJsonUtility()
        {
            var b = B();
            var errs = DataValidator.ValidateBalance(b);
            for (int n = 1; n <= 6; n++) errs.AddRange(DataValidator.ValidateLevel(b, L(n)));
            errs.AddRange(Progression.Validate(C(), b));
            Assert.IsEmpty(errs, string.Join(" | ", errs));
        }

        [Test] public void RecorridosYEtapasSeLeenEnteros()
        {
            Assert.AreEqual(1, LevelGeometry.RoutePoints(L(1)).Count);
            Assert.AreEqual(2, LevelGeometry.RoutePoints(L(5)).Count);
            Assert.AreEqual(2, LevelGeometry.ArrivalDirections(L(5)).Count, "el nivel 5 tiene dos llegadas a la base");
            Assert.AreEqual(4, LevelGeometry.RoutePoints(L(6)).Count);
            Assert.AreEqual(2, LevelGeometry.Spawns(L(6)).Count);
            var inf = B().towers.First(t => t.id == "infernal");
            Assert.AreEqual(4, inf.levels[0].rampDps.Length);
            Assert.IsFalse(string.IsNullOrEmpty(L(6).waves.Last().lanes));
        }

        [Test] public void LaPartidaRecibeLasMejorasDeLaTiendaYLaTanda()
        {
            var p = new Progression(C(), new SaveData());
            for (int n = 1; n <= 3; n++) p.ApplyVictory($"m1_n{n}", 3, 100);
            Assert.IsTrue(p.ChooseTanda("arqueras"));
            Assert.IsTrue(p.Buy("mortero_carga"));
            var m = new Match(B(), L(4), p.Loadout(false));
            Assert.AreEqual(25f * 1.15f, m.GetTowerType("arqueras").levels[0].damage, 0.01f);
            Assert.AreEqual(90f * 1.2f, m.GetTowerType("mortero").levels[0].damage, 0.01f);
            Assert.IsNull(m.GetTowerType("infernal"), "el Infernal todavía no está desbloqueado");
        }

        [Test] public void ElGuardadoIdaYVuelta()
        {
            var p = new Progression(C(), new SaveData());
            p.ApplyVictory("m1_n1", 2, 70);
            p.MarkTowerSeen("mortero");
            var json = JsonUtility.ToJson(p.Save);
            var back = new Progression(C(), JsonUtility.FromJson<SaveData>(json));
            Assert.AreEqual(p.Currency, back.Currency);
            Assert.AreEqual(2, back.StarsOf("m1_n1"));
            Assert.IsTrue(back.IsTowerUnlocked("mortero"));
            Assert.IsTrue(back.IsLevelUnlocked("m1_n2"));
            Assert.IsTrue(back.TowerSeen("mortero"));
        }

        [Test] public void UnaPartidaDelNivel6ConTodasLasTorresEsDeterminista()
        {
            string Once()
            {
                var b = B(); b.economy.startGold = 2000;
                var m = new Match(b, L(6), new MatchOptions());
                var log = new List<SimEvent>();
                m.Begin();
                m.TryBuild("mortero", new Vec2(-8f, 8f), out _, out _);
                m.TryBuild("electrica", new Vec2(2f, 3f), out _, out _);
                m.TryBuild("infernal", new Vec2(-1f, -3f), out _, out _);
                m.TryBuild("oro", new Vec2(-18f, 5f), out _, out _);
                m.TryBuild("lanzallamas", new Vec2(4f, -3f), out _, out _);
                m.TryBuild("bombardera", new Vec2(11f, 3f), out _, out _);
                for (int i = 0; i < 60 * 120 && !m.Ended; i++)
                {
                    foreach (var t in m.Towers.ToList()) if (t.Full) m.TryCollect(t.Id, out _, out _);
                    m.Tick(); m.DrainEvents(log);
                }
                return string.Join("\n", log.Select(e => e.ToString()));
            }
            var a = Once();
            Assert.Greater(a.Length, 1000);
            Assert.AreEqual(a, Once());
        }
    }
}
