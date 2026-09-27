using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClashDefense.Core;
using NUnit.Framework;
using UnityEngine;

namespace ClashDefense.Tests
{
    /// <summary>
    /// Las pruebas clave de Tools/ClashDefenseSim, corridas dentro de Unity: además de las reglas,
    /// prueban que JsonUtility lee los datos igual que System.Text.Json.
    /// </summary>
    public class CoreTests
    {
        static string DataPath(string f) => Path.Combine(Application.dataPath, "ClashDefense/Data", f);
        static BalanceData B() => JsonUtility.FromJson<BalanceData>(File.ReadAllText(DataPath("balance_p0.json")));
        static LevelData L() => JsonUtility.FromJson<LevelData>(File.ReadAllText(DataPath("level_p0.json")));

        static Match Started(BalanceData b, List<SimEvent> log)
        {
            var m = new Match(b, L(), false);
            m.Begin();
            while (m.State == MatchState.Countdown) m.Tick();
            m.DrainEvents(log);
            return m;
        }

        static void Run(Match m, List<SimEvent> log, System.Func<bool> stop, float seconds = 900f)
        {
            int max = (int)(seconds / Match.Step);
            for (int i = 0; i < max && !stop(); i++) { m.Tick(); m.DrainEvents(log); }
        }

        [Test] public void LosDatosDelDoc05Validan()
        {
            var errors = DataValidator.Validate(B(), L());
            Assert.IsEmpty(errors, string.Join(" | ", errors));
            Assert.AreEqual("arqueras", L().tutorialTowerId);
            Assert.IsNotNull(L().tutorialHint);
        }

        [Test] public void LasOleadasSeExpandenComoElDoc05()
        {
            var b = B();
            var gold = b.enemies.ToDictionary(e => e.code, e => e.gold);
            int[] counts = { 10, 14, 22, 22, 32 }; int[] golds = { 200, 232, 376, 436, 650 };
            for (int w = 0; w < 5; w++)
            {
                var seq = WaveSequence.Expand(b.waves[w].sequence);
                Assert.AreEqual(counts[w], seq.Count, $"oleada {w + 1}");
                Assert.AreEqual(golds[w], seq.Sum(c => gold[c]), $"oro oleada {w + 1}");
            }
        }

        [Test] public void UnDuendeRecorreEn30Segundos()
        {
            var b = B(); b.waves = new[] { new WaveData { sequence = "D", spawnInterval = 1f } };
            var log = new List<SimEvent>();
            var m = Started(b, log);
            float spawn = log.First(e => e.Type == SimEventType.EnemySpawned).Time;
            Run(m, log, () => log.Any(e => e.Type == SimEventType.EnemyReachedBase));
            Assert.AreEqual(30f, log.First(e => e.Type == SimEventType.EnemyReachedBase).Time - spawn, 0.03f);
        }

        [Test] public void ElCanonNoEligeAereosYLasArquerasDescubrenElMetal()
        {
            var b = B(); b.economy.startGold = 1000; b.waves = new[] { new WaveData { sequence = "V A", spawnInterval = 2f } };
            var log = new List<SimEvent>();
            var m = Started(b, log);
            m.TryBuild("canon", new Vec2(0f, 3.5f), out var canon, out _);
            m.TryBuild("arqueras", new Vec2(-6f, 3.5f), out var arq, out _);
            Run(m, log, () => m.Ended);
            var v = log.First(e => e.Type == SimEventType.EnemySpawned && e.Text == "esbirro").EnemyId;
            Assert.IsFalse(log.Any(e => e.Type == SimEventType.Shot && e.TowerId == canon.Id && e.EnemyId == v), "el Cañón disparó a un aéreo");
            Assert.AreEqual(1, log.Count(e => e.Type == SimEventType.AttackImmune && e.TowerId == arq.Id && e.Int1 == 1));
        }

        [Test] public void LaMismaPartidaDaLosMismosEventos()
        {
            string Once()
            {
                var log = new List<SimEvent>();
                var m = new Match(B(), L(), false);
                m.Begin();
                m.TryBuild("arqueras", new Vec2(4.5f, 3.5f), out _, out _);
                for (int i = 0; i < 60 * 90 && !m.Ended; i++) { m.Tick(); m.DrainEvents(log); }
                return string.Join("\n", log.Select(e => e.ToString()));
            }
            Assert.AreEqual(Once(), Once());
        }

        [Test] public void EnPausaNadaAvanzaNiSeConstruye()
        {
            var log = new List<SimEvent>();
            var m = Started(B(), log);
            Assert.IsTrue(m.Pause());
            float t = m.ActiveTime;
            for (int i = 0; i < 300; i++) m.Tick();
            Assert.AreEqual(t, m.ActiveTime);
            Assert.IsFalse(m.TryBuild("arqueras", new Vec2(4.5f, 3.5f), out _, out var r));
            Assert.AreEqual(RejectReason.InvalidState, r);
        }
    }
}
