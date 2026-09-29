using System;
using System.Collections.Generic;

namespace ClashDefense.Core
{
    public enum MatchState { Start, Countdown, Tutorial, Wave, Interval, Paused, Victory, Defeat, Abandoned }

    public enum TutorialStep { None = 0, Base = 1, Path = 2, Gold = 3, SelectTower = 4, PlaceTower = 5, Income = 6, Goal = 7 }

    public enum RejectReason { None, NotEnoughGold, OutsideArea, OnPath, Blocked, Overlap, InvalidState, UnknownTower, MaxLevel, TutorialRestricted, UnknownTarget, NothingToCollect }

    /// <summary>Con qué entra el jugador a la partida (GDS-002.0): tutorial, torres desbloqueadas y mejoras permanentes.</summary>
    public sealed class MatchOptions
    {
        public bool Tutorial;
        /// <summary>Torres que puede usar, en el orden del balance. null = todas (P0).</summary>
        public ICollection<string> AllowedTowers;
        /// <summary>Mejoras permanentes por torre (tienda y tanda). null = ninguna.</summary>
        public IDictionary<string, List<StatMod>> Mods;
    }

    /// <summary>
    /// Una partida: máquina de estados (GDS-001.6) + mundo (GDS-001.1…5, GDS-002.4…6).
    /// Pura: sin UnityEngine, determinista, avanzada por pasos fijos. La presentación la alimenta y escucha sus eventos.
    /// </summary>
    public sealed class Match
    {
        /// <summary>Paso fijo de simulación (GDS-001.0). Constante técnica, no de balance.</summary>
        public const float Step = 1f / 60f;
        const float Eps = 1e-4f;

        readonly BalanceData balance;
        readonly LevelData level;
        readonly List<TowerTypeData> towerOrder = new List<TowerTypeData>();
        readonly Dictionary<string, TowerTypeData> towerTypes = new Dictionary<string, TowerTypeData>();
        readonly Dictionary<string, EnemyTypeData> enemyByCode = new Dictionary<string, EnemyTypeData>();
        readonly List<PathTrack> routes = new List<PathTrack>();
        readonly WaveData[] waves;
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Tower> towers = new List<Tower>();
        readonly List<Projectile> projectiles = new List<Projectile>();
        readonly List<FireZone> fires = new List<FireZone>();
        readonly List<SimEvent> events = new List<SimEvent>();
        readonly float referenceLength;
        readonly float burnTick;

        List<SpawnEntry> waveSchedule;  // programa exacto de la oleada en curso (WaveSchedule)
        readonly List<Vec2> routeStarts = new List<Vec2>();
        /// <summary>Tipos de torre que ya descubrieron que el metal los anula en este nivel (Doc 05 v2.0 §8.2).</summary>
        readonly HashSet<string> immuneKnown = new HashSet<string>();
        int nextSpawnIndex;
        int waveTicks;          // el reloj se cuenta en pasos enteros: sin deriva de coma flotante
        long activeTicks;
        int stateTicks;
        string lastCountdown;
        int nextEnemyId = 1, nextTowerId = 1, nextProjectileId = 1, nextFireId = 1;

        public BalanceData Balance => balance;
        public LevelData Level => level;
        /// <summary>Recorrido principal (el 0). Referencia de velocidad y del tutorial.</summary>
        public PathTrack Path => routes[0];
        public IReadOnlyList<PathTrack> Routes => routes;
        public Vec2 BasePosition => routes[0].End;
        public MatchState State { get; private set; } = MatchState.Start;
        /// <summary>Estado al que vuelve la pausa.</summary>
        public MatchState ResumeState { get; private set; }
        public TutorialStep Tutorial { get; private set; } = TutorialStep.None;
        public bool TutorialEnabled { get; }
        public bool TutorialPlayed { get; private set; }
        public int Gold { get; private set; }
        public int BaseHp { get; private set; }
        public int WaveNumber { get; private set; }
        public int WaveCount => waves.Length;
        /// <summary>Tiempo de partida que cuenta para la duración: cuenta regresiva, oleadas e intervalos. Sin tutorial ni pausa.</summary>
        public float ActiveTime => (float)(activeTicks * (double)Step);
        public float StateTimer => (float)(stateTicks * (double)Step);
        public string Result { get; private set; }
        public int Stars { get; private set; }
        public bool Ended => State == MatchState.Victory || State == MatchState.Defeat || State == MatchState.Abandoned;

        public IReadOnlyList<Enemy> Enemies => enemies;
        public IReadOnlyList<Tower> Towers => towers;
        public IReadOnlyList<Projectile> Projectiles => projectiles;
        public IReadOnlyList<FireZone> Fires => fires;
        /// <summary>Si ese tipo de torre ya descubrió en este nivel que el metal lo anula (Doc 05 v2.0 §8.2).</summary>
        public bool ImmuneKnown(string towerTypeId) => immuneKnown.Contains(towerTypeId);
        /// <summary>Las torres que el jugador puede usar en ESTA partida, con sus mejoras permanentes aplicadas.</summary>
        public IReadOnlyList<TowerTypeData> TowerTypes => towerOrder;

        public Match(BalanceData balance, LevelData level, bool tutorial) : this(balance, level, new MatchOptions { Tutorial = tutorial }) { }

        public Match(BalanceData balance, LevelData level, MatchOptions options)
        {
            options = options ?? new MatchOptions();
            var errors = DataValidator.Validate(balance, level);
            if (errors.Count > 0) throw new InvalidOperationException("Datos inválidos:\n - " + string.Join("\n - ", errors));
            this.balance = balance;
            this.level = level;
            foreach (var pts in LevelGeometry.RoutePoints(level)) { routes.Add(new PathTrack(pts)); routeStarts.Add(pts[0]); }
            referenceLength = routes[0].Length;
            waves = level.waves != null && level.waves.Length > 0 ? level.waves : balance.waves;
            burnTick = balance.timing.burnTick;   // el validador lo exige > 0 cuando hay torres que queman
            foreach (var t in balance.towers)
            {
                if (options.AllowedTowers != null && !options.AllowedTowers.Contains(t.id)) continue;
                List<StatMod> mods = null;
                if (options.Mods != null) options.Mods.TryGetValue(t.id, out mods);
                var applied = StatMods.Apply(t, mods);
                towerOrder.Add(applied);
                towerTypes[t.id] = applied;
            }
            foreach (var e in balance.enemies) enemyByCode[e.code] = e;
            TutorialEnabled = options.Tutorial && !string.IsNullOrEmpty(level.tutorialTowerId) && towerTypes.ContainsKey(level.tutorialTowerId);
            // Doc 05 v2.0 §5.1: la primera partida del Nivel 1 (la del tutorial) arranca con menos oro
            Gold = TutorialEnabled && balance.economy.tutorialStartGold > 0 ? balance.economy.tutorialStartGold : balance.economy.startGold;
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
            var wd = waves[n - 1];
            waveSchedule = WaveSchedule.Build(wd, balance.waveRules, routeStarts);
            nextSpawnIndex = 0;
            waveTicks = 0;
            SetState(MatchState.Wave);
            Emit(new SimEvent { Type = SimEventType.WaveStarted, Int1 = n, Int2 = WaveCount });
            SpawnDue();
        }

        void SpawnDue()
        {
            double waveTime = waveTicks * (double)Step;
            while (nextSpawnIndex < waveSchedule.Count && waveSchedule[nextSpawnIndex].Time <= waveTime + Eps)
            {
                var entry = waveSchedule[nextSpawnIndex++];
                var type = enemyByCode[entry.Code];
                int route = entry.Route;
                var track = routes[route];
                var e = new Enemy
                {
                    Id = nextEnemyId++,
                    Wave = WaveNumber,
                    Route = route,
                    Type = type,
                    Layer = type.layer == "air" ? Layer.Air : Layer.Ground,
                    Hp = type.hp,
                    Armor = type.armor,
                    Distance = 0f,
                    Remaining = track.Length,
                    Speed = referenceLength / type.travelTime,
                    Position = track.Start,
                };
                enemies.Add(e);
                Emit(new SimEvent { Type = SimEventType.EnemySpawned, EnemyId = e.Id, Text = type.id, Int1 = WaveNumber, Int2 = route, Pos = e.Position });
            }
        }

        void CheckWaveCleared()
        {
            if (nextSpawnIndex < waveSchedule.Count) return;
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
                var track = routes[e.Route];
                e.Distance += e.Speed * dt;
                e.Position = track.Evaluate(e.Distance);
                e.Remaining = track.Length - e.Distance;
                if (e.Distance >= track.Length - Eps)
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

            // 3. fuego en el piso y quemaduras (antes que las torres: el daño de este paso ya cuenta)
            UpdateFires(dt);
            UpdateBurns(dt);

            // 4. torres (en orden de construcción)
            for (int i = 0; i < towers.Count; i++)
            {
                var t = towers[i];
                if (t.Type.attack == AttackKind.Gold) { Produce(t, dt); continue; }
                if (t.PulsesLeft > 0) UpdateBurst(t, dt);
                if (t.Cooldown > 0f)
                {
                    t.Cooldown -= dt;
                    if (t.Cooldown > Eps) continue;
                }
                bool fired;
                switch (t.Type.attack)
                {
                    case AttackKind.Inferno: fired = FireInferno(t); break;
                    case AttackKind.Chain: fired = FireChain(t); break;
                    case AttackKind.Flame: fired = FireFlame(t); break;
                    case AttackKind.Mortar: fired = FireMortar(t); break;
                    default:
                        {
                            var target = FindTarget(t);
                            fired = target != null;
                            if (fired) Fire(t, target);
                            break;
                        }
                }
                if (!fired) { t.Cooldown = 0f; continue; }
                t.Cooldown += t.Stats.interval;
            }

            // 5. proyectiles
            for (int i = 0; i < projectiles.Count; i++)
            {
                var p = projectiles[i];
                if (p.Kind == ProjectileKind.Lob)
                {
                    if (p.JustFired) { p.JustFired = false; continue; }   // el vuelo cuenta desde el tick siguiente: cae a los 1,1 s exactos (GDS-002.4)
                    p.Elapsed += dt;
                    float k = p.Progress;
                    p.Position = p.Start + (p.LastTargetPosition - p.Start) * k;
                    if (p.Elapsed >= p.FlightTime - Eps) { p.Position = p.LastTargetPosition; Impact(p, null); }
                    continue;
                }
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
            var track = routes[e.Route];
            e.Alive = false;
            e.Arrived = true;
            e.Position = track.End;
            e.Remaining = 0f;
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

        bool InReach(Tower t, Enemy e)
        {
            var s = t.Stats;
            float d2 = Vec2.SqrDistance(t.Position, e.Position);
            if (d2 > s.range * s.range) return false;
            if (s.minRange > 0f && d2 < s.minRange * s.minRange) return false;
            return true;
        }

        bool ValidFor(Tower t, Enemy e) => e.Alive && CanTarget(t.Type, e.Layer) && !(e.Armored && immuneKnown.Contains(t.Type.id)) && InReach(t, e);

        /// <summary>Objetivo válido con menor distancia restante hasta SU llegada (Doc 03 §14); empate: el que apareció antes (S1).</summary>
        public Enemy FindTarget(Tower t)
        {
            Enemy best = null;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!ValidFor(t, e)) continue;
                if (best == null || e.Remaining < best.Remaining - Eps || (Math.Abs(e.Remaining - best.Remaining) <= Eps && e.Id < best.Id)) best = e;
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
                Kind = ProjectileKind.Homing,
                TargetId = target.Id,
                Start = t.Position,
                Position = t.Position,
                LastTargetPosition = target.Position,
                Speed = t.Type.projectileSpeed,
                Damage = t.Stats.damage,
                AreaRadius = t.Type.attack == AttackKind.Area ? t.Stats.areaRadius : 0f,
            };
            projectiles.Add(p);
            Emit(new SimEvent { Type = SimEventType.Shot, TowerId = t.Id, EnemyId = target.Id, Int1 = p.Id, Text = t.Type.id, Pos = t.Position, Aim = target.Position });
        }

        // ---- Mortero: fija la posición del objetivo al disparar y no la corrige (Doc 04 §6.1)
        bool FireMortar(Tower t)
        {
            var target = FindTarget(t);
            if (target == null) return false;
            var s = t.Stats;
            var p = new Projectile
            {
                Id = nextProjectileId++,
                TowerId = t.Id,
                TowerType = t.Type,
                Kind = ProjectileKind.Lob,
                TargetId = 0,
                Start = t.Position,
                Position = t.Position,
                LastTargetPosition = target.Position,
                Damage = s.damage,
                AreaRadius = s.areaRadius,
                FlightTime = s.flightTime,
                JustFired = true,
            };
            projectiles.Add(p);
            Emit(new SimEvent { Type = SimEventType.Shot, TowerId = t.Id, EnemyId = target.Id, Int1 = p.Id, Text = t.Type.id, Pos = t.Position, Aim = target.Position, Float1 = s.flightTime });
            return true;
        }

        // ---- Eléctrica: primer objetivo en alcance, después salta dentro del radio del anterior; uno por enemigo (Doc 04 §6.3)
        readonly List<Enemy> chainHits = new List<Enemy>();
        bool FireChain(Tower t)
        {
            var first = FindTarget(t);
            if (first == null) return false;
            var s = t.Stats;
            chainHits.Clear();
            chainHits.Add(first);
            var from = t.Position;
            var current = first;
            float r2 = s.chainRadius * s.chainRadius;
            for (int link = 0; ; link++)
            {
                Emit(new SimEvent { Type = SimEventType.Shot, TowerId = t.Id, EnemyId = current.Id, Int1 = 0, Int2 = link + 1, Text = t.Type.id, Pos = from, Aim = current.Position });
                if (link >= s.chainJumps) break;
                Enemy next = null; float bestD = float.MaxValue;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.Alive || e.Armored || chainHits.Contains(e) || !CanTarget(t.Type, e.Layer)) continue; // los rebotes no buscan metal (GDS-002.4 S11)
                    float d2 = Vec2.SqrDistance(e.Position, current.Position);
                    if (d2 <= r2 && (d2 < bestD - Eps || (Math.Abs(d2 - bestD) <= Eps && next != null && e.Id < next.Id))) { bestD = d2; next = e; }
                }
                if (next == null) break;
                chainHits.Add(next);
                from = current.Position;
                current = next;
            }
            for (int i = 0; i < chainHits.Count; i++)
                if (chainHits[i].Alive) ApplyHit(chainHits[i], s.damage, t.Type, t, t.Id);
            return true;
        }

        // ---- Infernal: fija al más próximo a su llegada y lo mantiene; el daño sube por etapas; al cambiar vuelve a la primera (Doc 04 §6.4)
        bool FireInferno(Tower t)
        {
            var s = t.Stats;
            Enemy target = t.LockedId != 0 ? FindEnemy(t.LockedId) : null;
            if (target != null && !(target.Alive && CanTarget(t.Type, target.Layer) && InReach(t, target))) target = null;
            if (target == null)
            {
                // la fijación terminó: nueva búsqueda y la potencia vuelve a la etapa inicial
                var next = FindTarget(t);
                t.LockTime = 0f;
                t.LockImmuneShown = false;
                t.LockedId = next != null ? next.Id : 0;
                target = next;
                if (target == null) return false;
            }
            int stage = t.InfernoStage;
            float dmg = s.rampDps[stage] * s.interval;
            Emit(new SimEvent { Type = SimEventType.Shot, TowerId = t.Id, EnemyId = target.Id, Int1 = 0, Int2 = stage + 1, Text = t.Type.id, Pos = t.Position, Aim = target.Position });
            if (target.Armored && !t.InfernoMax)
            {
                // contra metal las primeras etapas no dañan, pero la torre sigue acumulando (no se registra como inmunidad descubierta)
                if (!t.LockImmuneShown)
                {
                    t.LockImmuneShown = true;
                    // Int2 = 1: es calentamiento, no inmunidad; el registro no lo cuenta como disparo desperdiciado (MET-002.7)
                    Emit(new SimEvent { Type = SimEventType.AttackImmune, EnemyId = target.Id, TowerId = t.Id, Int1 = 0, Int2 = 1, Text = t.Type.id, Pos = target.Position });
                }
            }
            else ApplyHit(target, dmg, t.Type, t, t.Id, recordImmunity: false);
            t.LockTime += s.interval;
            return true;
        }

        // ---- Lanzallamas: fija el punto del objetivo y lanza una ráfaga lineal; quema y deja fuego en el piso (Doc 04 §6.6).
        // Con pulseInterval > 0 la ráfaga son pulsos (Doc 05 v2.0 §6.7): el primero sale ya, el resto cada pulseInterval, sobre la misma línea.
        bool FireFlame(Tower t)
        {
            var target = FindTarget(t);
            if (target == null) return false;
            var s = t.Stats;
            var dir = target.Position - t.Position;
            float len = dir.Magnitude;
            if (len < 1e-4f) dir = new Vec2(1f, 0f); else dir = dir * (1f / len);
            var a = t.Position + dir * t.Type.footprintRadius;
            var b = t.Position + dir * s.range;
            float half = s.flameWidth * 0.5f;
            Emit(new SimEvent { Type = SimEventType.Shot, TowerId = t.Id, EnemyId = target.Id, Int1 = 0, Text = t.Type.id, Pos = a, Aim = b, Float1 = s.burnDuration, Float2 = s.flameWidth });
            if (s.pulseInterval > 0f && s.burstDuration > 0f)
            {
                t.BurstA = a;
                t.BurstB = b;
                t.PulsesLeft = Math.Max(1, (int)Math.Round(s.burstDuration / s.pulseInterval));
                t.PulseTimer = 0f;
                Pulse(t);
            }
            else FlameHit(t, a, b, half);
            if (s.burnDuration > 0f)
                fires.Add(new FireZone { Id = nextFireId++, TowerId = t.Id, TowerType = t.Type, A = a, B = b, HalfWidth = half, TimeLeft = s.burnDuration, Duration = s.burnDuration, BurnDps = s.burnDps });
            return true;
        }

        void UpdateBurst(Tower t, float dt)
        {
            t.PulseTimer -= dt;
            if (t.PulseTimer <= Eps) Pulse(t);
        }

        void Pulse(Tower t)
        {
            var s = t.Stats;
            FlameHit(t, t.BurstA, t.BurstB, s.flameWidth * 0.5f);
            t.PulsesLeft--;
            t.PulseTimer += s.pulseInterval;
        }

        void FlameHit(Tower t, Vec2 a, Vec2 b, float half)
        {
            var s = t.Stats;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!e.Alive || !CanTarget(t.Type, e.Layer)) continue;
                if (e.Armored && immuneKnown.Contains(t.Type.id)) continue; // ya sabe que el metal lo anula (§8.2)
                if (Vec2.DistanceToSegment(e.Position, a, b) > half) continue;
                ApplyHit(e, s.damage, t.Type, t, t.Id);
                if (e.Alive && !e.Armored) ApplyBurn(e, s.burnDps, s.burnDuration, t);
            }
        }

        void ApplyBurn(Enemy e, float dps, float duration, Tower source)
        {
            if (dps <= 0f || duration <= 0f || e.Armored) return; // el metal no se quema (Doc 04 §9)
            bool was = e.Burning;
            e.BurnTime = duration;                                              // no se acumula: reinicia la duración
            if (!was || dps >= e.BurnDps)
            {
                // con dos potencias manda la mayor, y la baja se acredita a quien puso esa potencia (GDS-002.4 S14)
                e.BurnDps = dps;
                e.BurnTowerId = source.Id;
                e.BurnSource = source.Type;
            }
            if (!was) { e.BurnTick = 0f; Emit(new SimEvent { Type = SimEventType.BurnStarted, EnemyId = e.Id, TowerId = source.Id, Text = source.Type.id, Pos = e.Position, Float1 = e.BurnDps }); }
        }

        void UpdateFires(float dt)
        {
            for (int f = fires.Count - 1; f >= 0; f--)
            {
                var z = fires[f];
                z.TimeLeft -= dt;
                if (z.TimeLeft <= 0f) { fires.RemoveAt(f); continue; }
                var src = FindTower(z.TowerId);
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.Alive || e.Layer != Layer.Ground || e.Armored) continue; // el fuego del piso prende a los terrestres
                    if (Vec2.DistanceToSegment(e.Position, z.A, z.B) > z.HalfWidth) continue;
                    if (src != null) ApplyBurn(e, z.BurnDps, z.Duration, src);
                    else
                    {
                        // la torre que dejó el fuego ya no existe (vendida): misma regla que ApplyBurn, con su tipo y su id
                        bool was = e.Burning;
                        e.BurnTime = z.Duration;
                        if (!was || z.BurnDps >= e.BurnDps) { e.BurnDps = z.BurnDps; e.BurnSource = z.TowerType; e.BurnTowerId = z.TowerId; }
                        if (!was) { e.BurnTick = 0f; Emit(new SimEvent { Type = SimEventType.BurnStarted, EnemyId = e.Id, TowerId = z.TowerId, Text = z.TowerType.id, Pos = e.Position, Float1 = e.BurnDps }); }
                    }
                }
            }
        }

        void UpdateBurns(float dt)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!e.Alive || !e.Burning) continue;
                if (e.Armored) { e.BurnTime = 0f; continue; }
                e.BurnTime -= dt;
                e.BurnTick += dt;
                if (e.BurnTick >= burnTick - Eps)
                {
                    e.BurnTick -= burnTick;
                    float dmg = e.BurnDps * burnTick;
                    float hpEff = Math.Min(dmg, e.Hp);
                    e.Hp -= dmg;
                    Emit(new SimEvent { Type = SimEventType.EnemyDamaged, EnemyId = e.Id, TowerId = e.BurnTowerId, Float1 = hpEff, Int1 = 1, Text = e.BurnSource != null ? e.BurnSource.id : "", Pos = e.Position });
                    if (e.Hp <= Eps) Kill(e, e.BurnTowerId);
                }
                if (e.BurnTime <= 0f) { e.BurnTime = 0f; e.BurnTick = 0f; }
            }
        }

        // ---- Torre de oro: produce hasta su capacidad y espera el clic (Doc 04 §6.5)
        void Produce(Tower t, float dt)
        {
            var s = t.Stats;
            if (t.Stored >= s.goldCapacity - Eps) return;   // lleno: deja de producir (el ciclo no avanza)
            if (s.goldCycle > 0f)
            {
                // por ciclos (Doc 05 v2.0 §6.6): suma goldPerCycle al completar cada ciclo
                t.CycleTime += dt;
                if (t.CycleTime < s.goldCycle - Eps) return;
                t.CycleTime -= s.goldCycle;
                t.Stored = Math.Min(s.goldCapacity, t.Stored + s.goldPerCycle);
            }
            else t.Stored = Math.Min(s.goldCapacity, t.Stored + s.goldPerSecond * dt);
            if (t.Stored >= s.goldCapacity - Eps && !t.Full)
            {
                t.Full = true;
                Emit(new SimEvent { Type = SimEventType.GoldStoredFull, TowerId = t.Id, Text = t.Type.id, Pos = t.Position });
            }
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

        /// <summary>La única función de daño directo (GDS-001.4). La quemadura descuenta vida en UpdateBurns.</summary>
        void ApplyHit(Enemy e, float damage, TowerTypeData type, Tower tower, int towerId, bool recordImmunity = true)
        {
            if (e.Armor > 0f)
            {
                float eff = type.metalEfficiency;
                if (eff <= 0f)
                {
                    // el descubrimiento es del tipo de torre y dura el nivel (Doc 05 v2.0 §8.2)
                    bool discovery = recordImmunity && tower != null && immuneKnown.Add(type.id);
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
                    if (e.Type.travelTimeExposed > 0f) e.Speed = referenceLength / e.Type.travelTimeExposed;
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
            if (!e.Alive) return;
            e.Alive = false;
            e.Hp = 0f;
            e.BurnTime = 0f;
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
        // Con tutorial, la cuenta regresiva no abre la construcción: el nivel 1 enseña las acciones antes de habilitarlas
        // (Doc 03 §6, excepción del tutorial; GDS-001.6, regla 1 enmendada). Sin tutorial rige el Doc 03 §5.
        bool ActionsOpen => (State == MatchState.Countdown && !TutorialEnabled) || State == MatchState.Wave || State == MatchState.Interval;

        /// <summary>Si el jugador puede elegir ese tipo de torre ahora (la presentación pregunta; no repite la regla).</summary>
        public bool CanSelectTower(string towerTypeId)
        {
            if (Ended || GetTowerType(towerTypeId) == null) return false;
            if (State == MatchState.Tutorial)
                return (Tutorial == TutorialStep.SelectTower || Tutorial == TutorialStep.PlaceTower) && towerTypeId == level.tutorialTowerId;
            return ActionsOpen;
        }

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
            for (int i = 0; i < routes.Count; i++)
                if (routes[i].DistanceTo(pos) < level.pathWidth * 0.5f + r) return RejectReason.OnPath;
            if (Vec2.Distance(pos, BasePosition) < level.baseRadius + r) return RejectReason.OnPath;
            if (level.blocked != null)
                foreach (var c in level.blocked)
                    if (c != null && c.radius > 0f && Vec2.Distance(pos, c.Center) < c.radius + r) return RejectReason.Blocked;
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
            if (t.Type.attack == AttackKind.Gold)
            {
                if (t.Stored < t.Stats.goldCapacity - Eps) t.Full = false; // la capacidad nueva vuelve a producir
                t.CycleTime = 0f;                                           // la producción recomienza al terminar la mejora (§6.6)
            }
            Emit(new SimEvent { Type = SimEventType.TowerUpgraded, TowerId = t.Id, Text = t.Type.id, Int1 = cost, Int2 = t.Level, Pos = t.Position });
            Emit(new SimEvent { Type = SimEventType.GoldChanged, Int1 = -cost, Int2 = Gold, Text = "mejora", Pos = t.Position });
            reason = RejectReason.None;
            return true;
        }

        /// <summary>Recoger el oro guardado en una Torre de oro (Doc 04 §6.5: recolección manual con clic).</summary>
        public bool TryCollect(int towerId, out int amount, out RejectReason reason)
        {
            amount = 0;
            if (!ActionsOpen) return Reject(RejectReason.InvalidState, "recoger", out reason);
            var t = FindTower(towerId);
            if (t == null || t.Type.attack != AttackKind.Gold) return Reject(RejectReason.UnknownTarget, "recoger", out reason);
            amount = (int)Math.Floor(t.Stored + Eps);
            if (amount <= 0) return Reject(RejectReason.NothingToCollect, "recoger", out reason);
            Collect(t, amount);
            reason = RejectReason.None;
            return true;
        }

        void Collect(Tower t, int amount)
        {
            t.Stored = Math.Max(0f, t.Stored - amount);
            t.Full = false;
            Gold += amount;
            Emit(new SimEvent { Type = SimEventType.GoldCollected, TowerId = t.Id, Text = t.Type.id, Int1 = amount, Pos = t.Position });
            Emit(new SimEvent { Type = SimEventType.GoldChanged, Int1 = amount, Int2 = Gold, Text = "recoleccion", Pos = t.Position });
        }

        public bool TrySell(int towerId, out int refund, out RejectReason reason)
        {
            refund = 0;
            if (!ActionsOpen) return Reject(RejectReason.InvalidState, "vender", out reason);
            var t = FindTower(towerId);
            if (t == null) return Reject(RejectReason.UnknownTarget, "vender", out reason);
            // el oro guardado en una Torre de oro se entrega al venderla (GDS-002.4 S16; Doc 04 §6.5 lo deja pendiente)
            if (t.Type.attack == AttackKind.Gold)
            {
                int stored = (int)Math.Floor(t.Stored + Eps);
                if (stored > 0) Collect(t, stored);
            }
            refund = SellRefund(t);
            Gold += refund;
            t.Sold = true;
            towers.Remove(t);
            if (t.LockedId != 0) t.LockedId = 0;
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
        public PathTrack RouteOf(Enemy e) => routes[e.Route];
    }
}
