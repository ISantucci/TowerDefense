using System;
using System.Collections.Generic;

namespace ClashDefense.Core
{
    public enum MatchState { Start, Countdown, Tutorial, Wave, Interval, Paused, Victory, Defeat, Abandoned }

    public enum TutorialStep { None = 0, Base = 1, Path = 2, Gold = 3, SelectTower = 4, PlaceTower = 5, Income = 6, Goal = 7 }

    public enum RejectReason { None, NotEnoughGold, OutsideArea, OnPath, Blocked, Overlap, InvalidState, UnknownTower, MaxLevel, TutorialRestricted, UnknownTarget }

    /// <summary>
    /// Una partida del Prototipo 0: máquina de estados (GDS-001.6) + mundo (GDS-001.1…5).
    /// Pura: sin UnityEngine, determinista, avanzada por pasos fijos. La presentación la alimenta y escucha sus eventos.
    /// </summary>
    public sealed class Match
    {
        /// <summary>Paso fijo de simulación (GDS-001.0). Constante técnica, no de balance.</summary>
        public const float Step = 1f / 60f;
        const float Eps = 1e-4f;

        readonly BalanceData balance;
        readonly LevelData level;
        readonly Dictionary<string, TowerTypeData> towerTypes = new Dictionary<string, TowerTypeData>();
        readonly Dictionary<string, EnemyTypeData> enemyByCode = new Dictionary<string, EnemyTypeData>();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Tower> towers = new List<Tower>();
        readonly List<Projectile> projectiles = new List<Projectile>();
        readonly List<SimEvent> events = new List<SimEvent>();

        List<EnemyTypeData> waveSeq;
        float waveSpawnInterval;
        int nextSpawnIndex;
        int waveTicks;          // el reloj se cuenta en pasos enteros: sin deriva de coma flotante
        long activeTicks;
        int stateTicks;
        string lastCountdown;
        int nextEnemyId = 1, nextTowerId = 1, nextProjectileId = 1;

        public BalanceData Balance => balance;
        public LevelData Level => level;
        public PathTrack Path { get; }
        public MatchState State { get; private set; } = MatchState.Start;
        /// <summary>Estado al que vuelve la pausa.</summary>
        public MatchState ResumeState { get; private set; }
        public TutorialStep Tutorial { get; private set; } = TutorialStep.None;
        public bool TutorialEnabled { get; }
        public bool TutorialPlayed { get; private set; }
        public int Gold { get; private set; }
        public int BaseHp { get; private set; }
        public int WaveNumber { get; private set; }
        public int WaveCount => balance.waves.Length;
        /// <summary>Tiempo de partida que cuenta para la duración: cuenta regresiva, oleadas e intervalos. Sin tutorial ni pausa.</summary>
        public float ActiveTime => (float)(activeTicks * (double)Step);
        public float StateTimer => (float)(stateTicks * (double)Step);
        public string Result { get; private set; }
        public int Stars { get; private set; }
        public bool Ended => State == MatchState.Victory || State == MatchState.Defeat || State == MatchState.Abandoned;

        public IReadOnlyList<Enemy> Enemies => enemies;
        public IReadOnlyList<Tower> Towers => towers;
        public IReadOnlyList<Projectile> Projectiles => projectiles;
        public IEnumerable<TowerTypeData> TowerTypes => balance.towers;

        public Match(BalanceData balance, LevelData level, bool tutorial)
        {
            var errors = DataValidator.Validate(balance, level);
            if (errors.Count > 0) throw new InvalidOperationException("Datos inválidos:\n - " + string.Join("\n - ", errors));
            this.balance = balance;
            this.level = level;
            Path = new PathTrack(level.path);
            foreach (var t in balance.towers) towerTypes[t.id] = t;
            foreach (var e in balance.enemies) enemyByCode[e.code] = e;
            TutorialEnabled = tutorial && !string.IsNullOrEmpty(level.tutorialTowerId) && towerTypes.ContainsKey(level.tutorialTowerId);
            Gold = balance.economy.startGold;
            BaseHp = balance.economy.baseHp;
        }

        // ------------------------------------------------------------------ eventos
        void Emit(SimEvent e) { e.Time = ActiveTime; events.Add(e); }

        /// <summary>Pasa los eventos acumulados a 'into' y los borra.</summary>
        public void DrainEvents(List<SimEvent> into) { into.AddRange(events); events.Clear(); }

        void SetState(MatchState s)
        {
            var prev = State;
            State = s;
            stateTicks = 0;
            Emit(new SimEvent { Type = SimEventType.StateChanged, Text = s.ToString(), Text2 = prev.ToString() });
        }

        // ------------------------------------------------------------------ comandos de flujo
        public void Begin()
        {
            if (State != MatchState.Start) return;
            Emit(new SimEvent { Type = SimEventType.GoldChanged, Int1 = Gold, Int2 = Gold, Text = "inicial" });
            SetState(MatchState.Countdown);
            lastCountdown = null;
            UpdateCountdownLabel();
        }

        public bool Pause()
        {
            if (State != MatchState.Countdown && State != MatchState.Tutorial && State != MatchState.Wave && State != MatchState.Interval) return false;
            ResumeState = State;
            var t = stateTicks;
            SetState(MatchState.Paused);
            stateTicks = t; // la pausa no consume el temporizador del estado
            return true;
        }

        public bool Resume()
        {
            if (State != MatchState.Paused) return false;
            var t = stateTicks;
            SetState(ResumeState);
            stateTicks = t;
            return true;
        }

        public void Abandon()
        {
            if (Ended) return;
            EndMatch(MatchState.Abandoned, "abandono");
        }

        public void TutorialNext()
        {
            if (State != MatchState.Tutorial) return;
            switch (Tutorial)
            {
                case TutorialStep.Base: SetTutorial(TutorialStep.Path); break;
                case TutorialStep.Path: SetTutorial(TutorialStep.Gold); break;
                case TutorialStep.Gold: SetTutorial(TutorialStep.SelectTower); break;
                case TutorialStep.Income: SetTutorial(TutorialStep.Goal); break;
                case TutorialStep.Goal:
                    SetTutorial(TutorialStep.None);
                    StartWave(1);
                    break;
            }
        }

        /// <summary>La presentación avisa qué torre eligió el jugador (paso 4 del tutorial).</summary>
        public void NotifyTowerSelected(string towerTypeId)
        {
            if (State == MatchState.Tutorial && Tutorial == TutorialStep.SelectTower && towerTypeId == level.tutorialTowerId)
                SetTutorial(TutorialStep.PlaceTower);
        }

        void SetTutorial(TutorialStep s)
        {
            Tutorial = s;
            Emit(new SimEvent { Type = SimEventType.TutorialStep, Int1 = (int)s });
        }

        // ------------------------------------------------------------------ reloj
        /// <summary>Avanza un paso fijo (Step) de tiempo de partida.</summary>
        public void Tick()
        {
            const float dt = Step;
            switch (State)
            {
                case MatchState.Countdown:
                    activeTicks++;
                    stateTicks++;
                    UpdateCountdownLabel();
                    if (StateTimer >= 3f * balance.timing.countdownStep + balance.timing.defenseDuration - Eps)
                    {
                        if (TutorialEnabled)
                        {
                            TutorialPlayed = true;
                            SetState(MatchState.Tutorial);
                            SetTutorial(TutorialStep.Base);
                        }
                        else StartWave(1);
                    }
                    break;

                case MatchState.Wave:
                    activeTicks++;
                    stateTicks++;
                    UpdateWorld(dt);
                    if (State == MatchState.Wave) CheckWaveCleared();
                    break;

                case MatchState.Interval:
                    activeTicks++;
                    stateTicks++;
                    UpdateWorld(dt);
                    if (State == MatchState.Interval && StateTimer >= balance.timing.waveGap - Eps)
                        StartWave(WaveNumber + 1);
                    break;
                // Start, Tutorial, Paused y los finales: el tiempo de partida no corre.
            }
        }

        void UpdateCountdownLabel()
        {
            float s = balance.timing.countdownStep;
            string label = StateTimer < s - Eps ? "3" : StateTimer < 2 * s - Eps ? "2" : StateTimer < 3 * s - Eps ? "1" : "DEFENSE";
            if (label != lastCountdown)
            {
                lastCountdown = label;
                Emit(new SimEvent { Type = SimEventType.Countdown, Text = label });
            }
        }

        // ------------------------------------------------------------------ oleadas
        void StartWave(int n)
        {
            WaveNumber = n;
            var wd = balance.waves[n - 1];
            waveSeq = new List<EnemyTypeData>();
            foreach (var code in WaveSequence.Expand(wd.sequence)) waveSeq.Add(enemyByCode[code]);
            waveSpawnInterval = wd.spawnInterval;
            nextSpawnIndex = 0;
            waveTicks = 0;
            SetState(MatchState.Wave);
            Emit(new SimEvent { Type = SimEventType.WaveStarted, Int1 = n, Int2 = WaveCount });
            SpawnDue();
        }

        void SpawnDue()
        {
            double waveTime = waveTicks * (double)Step;
            while (nextSpawnIndex < waveSeq.Count && nextSpawnIndex * (double)waveSpawnInterval <= waveTime + Eps)
            {
                var type = waveSeq[nextSpawnIndex++];
                var e = new Enemy
                {
                    Id = nextEnemyId++,
                    Wave = WaveNumber,
                    Type = type,
                    Layer = type.layer == "air" ? Layer.Air : Layer.Ground,
                    Hp = type.hp,
                    Armor = type.armor,
                    Distance = 0f,
                    Speed = Path.Length / type.travelTime,
                    Position = Path.Start,
                };
                enemies.Add(e);
                Emit(new SimEvent { Type = SimEventType.EnemySpawned, EnemyId = e.Id, Text = type.id, Int1 = WaveNumber, Pos = e.Position });
            }
        }

        void CheckWaveCleared()
        {
            if (nextSpawnIndex < waveSeq.Count) return;
            for (int i = 0; i < enemies.Count; i++) if (enemies[i].Alive) return;
            Emit(new SimEvent { Type = SimEventType.WaveCleared, Int1 = WaveNumber, Int2 = WaveCount });
            if (WaveNumber >= WaveCount) EndMatch(MatchState.Victory, "victoria");
            else SetState(MatchState.Interval);
        }

        // ------------------------------------------------------------------ mundo
        void UpdateWorld(float dt)
        {
            // 1. movimiento y llegada (en orden de aparición)
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!e.Alive) continue;
                e.Distance += e.Speed * dt;
                e.Position = Path.Evaluate(e.Distance);
                if (e.Distance >= Path.Length - Eps)
                {
                    Arrive(e);
                    if (State == MatchState.Defeat) return; // la derrota manda en su paso
                }
            }

            // 2. apariciones de la oleada en curso (no se mueven en el paso en que nacen)
            if (State == MatchState.Wave)
            {
                waveTicks++;
                SpawnDue();
            }

            // 3. torres (en orden de construcción)
            for (int i = 0; i < towers.Count; i++)
            {
                var t = towers[i];
                if (t.Cooldown > 0f)
                {
                    t.Cooldown -= dt;
                    if (t.Cooldown > Eps) continue;
                }
                var target = FindTarget(t);
                if (target == null) { t.Cooldown = 0f; continue; }
                Fire(t, target);
                t.Cooldown += t.Stats.interval;
            }

            // 4. proyectiles
            for (int i = 0; i < projectiles.Count; i++)
            {
                var p = projectiles[i];
                var target = FindEnemy(p.TargetId);
                if (target != null && target.Alive) p.LastTargetPosition = target.Position;
                float step = p.Speed * dt;
                if (Vec2.Distance(p.Position, p.LastTargetPosition) <= step)
                {
                    p.Position = p.LastTargetPosition;
                    Impact(p, target);
                }
                else p.Position = Vec2.MoveTowards(p.Position, p.LastTargetPosition, step);
            }

            projectiles.RemoveAll(p => p.Done);
            enemies.RemoveAll(e => !e.Alive);
        }

        void Arrive(Enemy e)
        {
            e.Alive = false;
            e.Arrived = true;
            e.Position = Path.End;
            int before = BaseHp;
            BaseHp = Math.Max(0, BaseHp - e.Type.baseDamage);
            Emit(new SimEvent { Type = SimEventType.EnemyReachedBase, EnemyId = e.Id, Text = e.Type.id, Int1 = e.Type.baseDamage, Pos = e.Position });
            Emit(new SimEvent { Type = SimEventType.BaseDamaged, Int1 = before, Int2 = BaseHp, Text = e.Type.id, EnemyId = e.Id });
            if (BaseHp <= 0) EndMatch(MatchState.Defeat, "derrota");
        }

        Enemy FindEnemy(int id)
        {
            for (int i = 0; i < enemies.Count; i++) if (enemies[i].Id == id) return enemies[i];
            return null;
        }

        Tower FindTower(int id)
        {
            for (int i = 0; i < towers.Count; i++) if (towers[i].Id == id) return towers[i];
            return null;
        }

        static bool CanTarget(TowerTypeData t, Layer layer) => layer == Layer.Air ? t.targetsAir : t.targetsGround;

        /// <summary>Objetivo válido con menor distancia restante; empate: el que apareció antes (S1).</summary>
        public Enemy FindTarget(Tower t)
        {
            float r2 = t.Stats.range * t.Stats.range;
            Enemy best = null;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!e.Alive) continue;
                if (!CanTarget(t.Type, e.Layer)) continue;
                if (e.Armored && t.KnownImmune.Contains(e.Id)) continue;
                if (Vec2.SqrDistance(t.Position, e.Position) > r2) continue;
                if (best == null || e.Distance > best.Distance) best = e;
            }
            return best;
        }

        void Fire(Tower t, Enemy target)
        {
            var p = new Projectile
            {
                Id = nextProjectileId++,
                TowerId = t.Id,
                TowerType = t.Type,
                TargetId = target.Id,
                Position = t.Position,
                LastTargetPosition = target.Position,
                Speed = t.Type.projectileSpeed,
                Damage = t.Stats.damage,
                AreaRadius = t.Type.attack == "area" ? t.Stats.areaRadius : 0f,
            };
            projectiles.Add(p);
            Emit(new SimEvent { Type = SimEventType.Shot, TowerId = t.Id, EnemyId = target.Id, Int1 = p.Id, Text = t.Type.id, Pos = t.Position });
        }

        void Impact(Projectile p, Enemy target)
        {
            p.Done = true;
            Emit(new SimEvent { Type = SimEventType.ProjectileImpact, Int1 = p.Id, TowerId = p.TowerId, Pos = p.Position, Float1 = p.AreaRadius, Text = p.TowerType.id });
            var tower = FindTower(p.TowerId);
            if (p.AreaRadius > 0f)
            {
                float r2 = p.AreaRadius * p.AreaRadius;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.Alive || !CanTarget(p.TowerType, e.Layer)) continue;
                    if (Vec2.SqrDistance(e.Position, p.Position) <= r2) ApplyHit(e, p.Damage, p.TowerType, tower, p.TowerId);
                }
            }
            else if (target != null && target.Alive)
            {
                ApplyHit(target, p.Damage, p.TowerType, tower, p.TowerId);
            }
            // si el objetivo murió o llegó antes, el proyectil de un solo objetivo se pierde (S3)
        }

        /// <summary>La única función de daño (GDS-001.4).</summary>
        void ApplyHit(Enemy e, int damage, TowerTypeData type, Tower tower, int towerId)
        {
            if (e.Armor > 0f)
            {
                float eff = type.metalEfficiency;
                if (eff <= 0f)
                {
                    bool discovery = tower != null && tower.KnownImmune.Add(e.Id);
                    Emit(new SimEvent { Type = SimEventType.AttackImmune, EnemyId = e.Id, TowerId = towerId, Int1 = discovery ? 1 : 0, Text = type.id, Pos = e.Position });
                    return;
                }
                float dmg = damage * eff;
                float effective = Math.Min(dmg, e.Armor);
                e.Armor -= dmg;
                Emit(new SimEvent { Type = SimEventType.EnemyDamaged, EnemyId = e.Id, TowerId = towerId, Float2 = effective, Text = type.id, Pos = e.Position });
                if (e.Armor <= Eps)
                {
                    e.Armor = 0f; // el exceso no pasa a la vida
                    if (e.Type.travelTimeExposed > 0f) e.Speed = Path.Length / e.Type.travelTimeExposed;
                    Emit(new SimEvent { Type = SimEventType.ArmorBroken, EnemyId = e.Id, TowerId = towerId, Text = e.Type.id, Pos = e.Position });
                }
                return;
            }
            float hpEff = Math.Min(damage, e.Hp);
            e.Hp -= damage;
            Emit(new SimEvent { Type = SimEventType.EnemyDamaged, EnemyId = e.Id, TowerId = towerId, Float1 = hpEff, Text = type.id, Pos = e.Position });
            if (e.Hp <= Eps) Kill(e, towerId);
        }

        void Kill(Enemy e, int towerId)
        {
            e.Alive = false;
            e.Hp = 0f;
            Gold += e.Type.gold;
            Emit(new SimEvent { Type = SimEventType.EnemyKilled, EnemyId = e.Id, TowerId = towerId, Text = e.Type.id, Int1 = e.Type.gold, Pos = e.Position });
            Emit(new SimEvent { Type = SimEventType.GoldChanged, Int1 = e.Type.gold, Int2 = Gold, Text = "baja", Pos = e.Position });
        }

        void EndMatch(MatchState s, string result)
        {
            Result = result;
            Stars = s == MatchState.Victory ? ComputeStars(BaseHp) : 0;
            SetState(s);
            Emit(new SimEvent { Type = SimEventType.MatchEnded, Text = result, Int1 = Stars, Int2 = BaseHp });
        }

        public int ComputeStars(int hp)
        {
            if (hp <= 0) return 0;
            if (hp >= balance.stars.threeStarsMinHp) return 3;
            if (hp >= balance.stars.twoStarsMinHp) return 2;
            return 1;
        }

        // ------------------------------------------------------------------ acciones del jugador
        bool ActionsOpen => State == MatchState.Countdown || State == MatchState.Wave || State == MatchState.Interval;

        public TowerTypeData GetTowerType(string id) => id != null && towerTypes.TryGetValue(id, out var t) ? t : null;

        public int UpgradeCost(Tower t) => t.CanUpgrade ? (int)Math.Round(t.Type.cost * balance.economy.upgradeCostFactor) : 0;
        public int SellRefund(Tower t) => (int)Math.Floor(t.Invested * balance.economy.sellRefundFactor + Eps);

        /// <summary>Validación de una posición en el orden del GDS-001.2 (el primer fallo es el motivo).</summary>
        public RejectReason CheckPlacement(string towerTypeId, Vec2 pos, bool checkGold = true)
        {
            var type = GetTowerType(towerTypeId);
            if (type == null) return RejectReason.UnknownTower;
            if (checkGold && Gold < type.cost) return RejectReason.NotEnoughGold;
            return CheckGeometry(type, pos);
        }

        public RejectReason CheckGeometry(TowerTypeData type, Vec2 pos)
        {
            float r = type.footprintRadius;
            if (!level.buildArea.Contains(pos)) return RejectReason.OutsideArea;
            if (Path.DistanceTo(pos) < level.pathWidth * 0.5f + r) return RejectReason.OnPath;
            if (Vec2.Distance(pos, Path.End) < level.baseRadius + r) return RejectReason.OnPath;
            if (level.blocked != null)
                foreach (var c in level.blocked)
                    if (Vec2.Distance(pos, c.Center) < c.radius + r) return RejectReason.Blocked;
            for (int i = 0; i < towers.Count; i++)
                if (Vec2.Distance(pos, towers[i].Position) < towers[i].Type.footprintRadius + r) return RejectReason.Overlap;
            return RejectReason.None;
        }

        public bool TryBuild(string towerTypeId, Vec2 pos, out Tower built, out RejectReason reason)
        {
            built = null;
            bool tutorialBuild = State == MatchState.Tutorial && Tutorial == TutorialStep.PlaceTower;
            if (!ActionsOpen && !tutorialBuild) return Reject(RejectReason.InvalidState, "construir", out reason);
            if (tutorialBuild && towerTypeId != level.tutorialTowerId) return Reject(RejectReason.TutorialRestricted, "construir", out reason);
            reason = CheckPlacement(towerTypeId, pos);
            if (reason != RejectReason.None) return Reject(reason, "construir", out reason);

            var type = GetTowerType(towerTypeId);
            Gold -= type.cost;
            built = new Tower { Id = nextTowerId++, Type = type, Position = pos, Invested = type.cost };
            towers.Add(built);
            Emit(new SimEvent { Type = SimEventType.TowerBuilt, TowerId = built.Id, Text = type.id, Int1 = type.cost, Int2 = 1, Pos = pos });
            Emit(new SimEvent { Type = SimEventType.GoldChanged, Int1 = -type.cost, Int2 = Gold, Text = "construccion", Pos = pos });
            if (tutorialBuild) SetTutorial(TutorialStep.Income);
            return true;
        }

        public bool TryUpgrade(int towerId, out RejectReason reason)
        {
            if (!ActionsOpen) return Reject(RejectReason.InvalidState, "mejorar", out reason);
            var t = FindTower(towerId);
            if (t == null) return Reject(RejectReason.UnknownTarget, "mejorar", out reason);
            if (!t.CanUpgrade) return Reject(RejectReason.MaxLevel, "mejorar", out reason);
            int cost = UpgradeCost(t);
            if (Gold < cost) return Reject(RejectReason.NotEnoughGold, "mejorar", out reason);
            Gold -= cost;
            t.Level++;
            t.Invested += cost;
            Emit(new SimEvent { Type = SimEventType.TowerUpgraded, TowerId = t.Id, Text = t.Type.id, Int1 = cost, Int2 = t.Level, Pos = t.Position });
            Emit(new SimEvent { Type = SimEventType.GoldChanged, Int1 = -cost, Int2 = Gold, Text = "mejora", Pos = t.Position });
            reason = RejectReason.None;
            return true;
        }

        public bool TrySell(int towerId, out int refund, out RejectReason reason)
        {
            refund = 0;
            if (!ActionsOpen) return Reject(RejectReason.InvalidState, "vender", out reason);
            var t = FindTower(towerId);
            if (t == null) return Reject(RejectReason.UnknownTarget, "vender", out reason);
            refund = SellRefund(t);
            Gold += refund;
            t.Sold = true;
            towers.Remove(t);
            Emit(new SimEvent { Type = SimEventType.TowerSold, TowerId = t.Id, Text = t.Type.id, Int1 = refund, Int2 = t.Level, Pos = t.Position });
            Emit(new SimEvent { Type = SimEventType.GoldChanged, Int1 = refund, Int2 = Gold, Text = "venta", Pos = t.Position });
            reason = RejectReason.None;
            return true;
        }

        bool Reject(RejectReason r, string action, out RejectReason reason)
        {
            reason = r;
            Emit(new SimEvent { Type = SimEventType.ActionRejected, Text = r.ToString(), Text2 = action });
            return false;
        }

        public Tower GetTower(int id) => FindTower(id);
        public Enemy GetEnemy(int id) => FindEnemy(id);
    }
}
