using System;
using System.Collections.Generic;

namespace Srpg.Core.Combat
{
    public enum BattleOutcome
    {
        Ongoing,
        PlayerWon,
        PlayerLost,
        TimeUp,
    }

    public enum ActionKind
    {
        Wait,
        Attack,
        PickUp,
    }

    public struct UnitCommand
    {
        public int UnitId;
        public GridPos MoveTo;
        public ActionKind Action;
        public int TargetId;

        public static UnitCommand Wait(Unit u, GridPos moveTo) =>
            new UnitCommand { UnitId = u.Id, MoveTo = moveTo, Action = ActionKind.Wait, TargetId = -1 };

        public static UnitCommand Attack(Unit u, GridPos moveTo, Unit target) =>
            new UnitCommand { UnitId = u.Id, MoveTo = moveTo, Action = ActionKind.Attack, TargetId = target.Id };

        public static UnitCommand PickUp(Unit u, GridPos moveTo, Unit target) =>
            new UnitCommand { UnitId = u.Id, MoveTo = moveTo, Action = ActionKind.PickUp, TargetId = target.Id };
    }

    public struct AttackForecast
    {
        public int Hit;
        public int Damage;
        public int Strikes;
    }

    public struct MissionScore
    {
        public int Speed;
        public int Casualty;
        public int Total;
    }

    public sealed class Battle
    {
        public readonly BattleMap Map;
        public readonly BattleConfig Config;
        public readonly Rng Rng;
        public readonly int TurnLimit;
        public readonly List<Unit> Units = new List<Unit>();
        public readonly List<Corps> Corps = new List<Corps>();
        public readonly List<string> Log = new List<string>();

        public int Round { get; private set; } = 1;
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;

        private readonly List<int> turnOrder = new List<int>();
        private int orderIndex = -1;
        private readonly int[] occupant;
        private readonly Dictionary<int, Unit> unitById = new Dictionary<int, Unit>();
        private readonly Dictionary<int, Corps> corpsById = new Dictionary<int, Corps>();

        // Order lists are each side's corps in their pre-battle order (D37); the player side acts first.
        public Battle(BattleMap map, BattleConfig config, ulong seed, int turnLimit,
            IEnumerable<Unit> units, IList<Corps> playerOrder, IList<Corps> enemyOrder)
        {
            Map = map;
            Config = config;
            Rng = new Rng(seed);
            TurnLimit = turnLimit;
            occupant = new int[map.Width * map.Height];
            for (int i = 0; i < occupant.Length; i++) occupant[i] = -1;

            foreach (var u in units)
            {
                if (!map.InBounds(u.Pos)) throw new ArgumentException($"{u} 不在地图内");
                if (!config.Terrain(map.Get(u.Pos)).Passable) throw new ArgumentException($"{u} 站在不可通行的格子上");
                if (occupant[map.Index(u.Pos)] >= 0) throw new ArgumentException($"{u} 与其他单位重叠");
                u.Hp = u.Stats.MaxHp;
                u.Morale = config.MoraleMax;
                u.Status = UnitStatus.Active;
                Units.Add(u);
                unitById.Add(u.Id, u);
                occupant[map.Index(u.Pos)] = u.Id;
            }

            for (int i = 0; i < Math.Max(playerOrder.Count, enemyOrder.Count); i++)
            {
                if (i < playerOrder.Count) AddCorps(playerOrder[i]);
                if (i < enemyOrder.Count) AddCorps(enemyOrder[i]);
            }
        }

        private void AddCorps(Corps c)
        {
            Corps.Add(c);
            corpsById.Add(c.Id, c);
            turnOrder.Add(c.Id);
        }

        public Unit GetUnit(int id) => unitById[id];
        public Corps GetCorps(int id) => corpsById[id];
        public IReadOnlyList<int> TurnOrder => turnOrder;

        public Unit UnitAt(GridPos p)
        {
            int id = occupant[Map.Index(p)];
            return id < 0 ? null : unitById[id];
        }

        public Corps ActiveCorps =>
            Outcome == BattleOutcome.Ongoing && orderIndex >= 0 ? corpsById[turnOrder[orderIndex]] : null;

        public bool IsLeader(Unit u) => corpsById[u.CorpsId].LeaderId == u.Id;

        public IEnumerable<Unit> Members(Corps c)
        {
            foreach (int id in c.Members) yield return unitById[id];
        }

        public List<Unit> ControllableUnits(Corps c)
        {
            var list = new List<Unit>();
            foreach (var u in Members(c))
                if (u.Status == UnitStatus.Active && !u.HasActed) list.Add(u);
            return list;
        }

        public IEnumerable<Unit> CapturedBy(Side side)
        {
            foreach (var u in Units)
                if (u.Status == UnitStatus.Carried && unitById[u.CarriedById].Side == side)
                    yield return u;
        }

        // ---------- Turn flow ----------

        public void Start()
        {
            if (orderIndex >= 0) throw new InvalidOperationException("战斗已经开始");
            orderIndex = 0;
            ActivateCurrent();
        }

        public void EndActivation()
        {
            if (Outcome != BattleOutcome.Ongoing) return;
            Advance();
            ActivateCurrent();
        }

        private void Advance()
        {
            orderIndex++;
            if (orderIndex < turnOrder.Count) return;
            orderIndex = 0;
            Round++;
            if (Round > TurnLimit)
            {
                Outcome = BattleOutcome.TimeUp;
                Log.Add($"超过回合限制({TurnLimit} 轮),战斗结束");
            }
        }

        private void ActivateCurrent()
        {
            int skipped = 0;
            while (Outcome == BattleOutcome.Ongoing)
            {
                var corps = corpsById[turnOrder[orderIndex]];
                if (HasUnitInFight(corps))
                {
                    BeginCorpsActivation(corps);
                    if (Outcome != BattleOutcome.Ongoing) return;
                    if (ControllableUnits(corps).Count > 0) return;
                }
                if (++skipped > turnOrder.Count * (TurnLimit + 1)) throw new InvalidOperationException("无法找到可行动的兵团");
                Advance();
            }
        }

        private bool HasUnitInFight(Corps c)
        {
            foreach (var u in Members(c))
                if (u.InFight) return true;
            return false;
        }

        private void BeginCorpsActivation(Corps corps)
        {
            Log.Add($"— 第 {Round} 轮 · {corps.Name} 行动 —");
            foreach (var u in Members(corps))
            {
                if (!u.InFight) continue;
                u.HasActed = false;
                int recover = Config.MoraleRecoverBase + (AuraLeadership(u, u.Pos) > 0 ? Config.MoraleRecoverInAura : 0);
                u.Morale = Math.Min(Config.MoraleMax, u.Morale + recover);
            }
            foreach (var u in Members(corps))
            {
                if (u.Status != UnitStatus.Routed) continue;
                if (u.Morale >= Config.WaverThreshold)
                {
                    u.Status = UnitStatus.Active;
                    Log.Add($"{u.Name} 重新振作");
                    continue;
                }
                ResolveRout(u);
                if (Outcome != BattleOutcome.Ongoing) return;
            }
        }

        // Routed units are not controllable: flee, escape at the edge, or surrender when cornered (D20 ②).
        private void ResolveRout(Unit u)
        {
            u.HasActed = true;
            if (Map.EdgeDistance(u.Pos) == 0)
            {
                occupant[Map.Index(u.Pos)] = -1;
                u.Status = UnitStatus.Escaped;
                Log.Add($"{u.Name} 逃离战场");
                CheckOutcome();
                return;
            }

            int current = ThreatDistance(u, u.Pos);
            GridPos best = u.Pos;
            int bestThreat = current;
            int bestEdge = Map.EdgeDistance(u.Pos);
            foreach (var d in Destinations(u))
            {
                int threat = ThreatDistance(u, d);
                int edge = Map.EdgeDistance(d);
                if (threat > bestThreat || (threat == bestThreat && edge < bestEdge))
                {
                    best = d;
                    bestThreat = threat;
                    bestEdge = edge;
                }
            }

            if (bestThreat <= current && AdjacentToEnemyFighter(u))
            {
                u.Status = UnitStatus.Surrendered;
                Log.Add($"{u.Name} 被包围,投降");
                CheckOutcome();
                return;
            }
            if (best != u.Pos) MoveUnit(u, best);
        }

        private int ThreatDistance(Unit u, GridPos p)
        {
            int best = int.MaxValue;
            foreach (var e in Units)
                if (e.Side != u.Side && e.Status == UnitStatus.Active)
                    best = Math.Min(best, p.Manhattan(e.Pos));
            return best;
        }

        private bool AdjacentToEnemyFighter(Unit u)
        {
            foreach (var dir in GridPos.Directions)
            {
                var n = u.Pos + dir;
                if (!Map.InBounds(n)) continue;
                var other = UnitAt(n);
                if (other != null && other.Side != u.Side && other.Status == UnitStatus.Active) return true;
            }
            return false;
        }

        // ---------- Movement ----------

        public UnitStats EffectiveStats(Unit u)
        {
            var s = u.Stats;
            if (!u.IsCarrying) return s;
            var captive = unitById[u.CarryingId];
            int burden = Math.Min(100, captive.Stats.Bld * 100 / Math.Max(1, u.Stats.Bld));
            s.Mov = Math.Max(1, s.Mov - (Config.CarryMovePenaltyMax * burden + 50) / 100);
            int keep = 100 - Config.CarryStatPenaltyMaxPercent * burden / 100;
            s.Str = s.Str * keep / 100;
            s.Skl = s.Skl * keep / 100;
            s.Spd = s.Spd * keep / 100;
            return s;
        }

        public Dictionary<GridPos, int> ReachableTiles(Unit u) => Pathfinding.Reachable(this, u, EffectiveStats(u).Mov);

        public List<GridPos> Destinations(Unit u)
        {
            var list = new List<GridPos>();
            foreach (var kv in ReachableTiles(u))
            {
                int occ = occupant[Map.Index(kv.Key)];
                if (occ < 0 || occ == u.Id) list.Add(kv.Key);
            }
            return list;
        }

        // Enemies `u` could attack after moving to `from`.
        public List<Unit> AttackTargetsFrom(Unit u, GridPos from)
        {
            var list = new List<Unit>();
            foreach (var e in Units)
                if (e.Side != u.Side && e.InFight && InRange(u.Weapon, from, e.Pos)) list.Add(e);
            return list;
        }

        // Stunned or surrendered enemies `u` could pick up after moving to `from`.
        public List<Unit> PickUpTargetsFrom(Unit u, GridPos from)
        {
            var list = new List<Unit>();
            if (u.IsCarrying) return list;
            foreach (var e in Units)
                if (e.Side != u.Side && e.Capturable && from.Manhattan(e.Pos) == 1 && u.Stats.Bld >= e.Stats.Bld)
                    list.Add(e);
            return list;
        }

        private void MoveUnit(Unit u, GridPos to)
        {
            if (to == u.Pos) return;
            occupant[Map.Index(u.Pos)] = -1;
            occupant[Map.Index(to)] = u.Id;
            u.Pos = to;
            if (u.IsCarrying) unitById[u.CarryingId].Pos = to;
        }

        // ---------- Commands ----------

        public bool TryExecute(UnitCommand cmd, out string error)
        {
            error = Validate(cmd);
            if (error != null) return false;

            var u = unitById[cmd.UnitId];
            MoveUnit(u, cmd.MoveTo);
            switch (cmd.Action)
            {
                case ActionKind.Attack:
                    ResolveAttack(u, unitById[cmd.TargetId]);
                    break;
                case ActionKind.PickUp:
                    PickUp(u, unitById[cmd.TargetId]);
                    break;
            }
            u.HasActed = true;
            CheckOutcome();
            return true;
        }

        private string Validate(UnitCommand cmd)
        {
            if (Outcome != BattleOutcome.Ongoing) return "战斗已结束";
            if (!unitById.TryGetValue(cmd.UnitId, out var u)) return "单位不存在";
            var active = ActiveCorps;
            if (active == null || u.CorpsId != active.Id) return "该单位所属兵团不在行动中";
            if (u.Status != UnitStatus.Active) return "该单位无法行动";
            if (u.HasActed) return "该单位本轮已行动";
            if (!Map.InBounds(cmd.MoveTo) || !Destinations(u).Contains(cmd.MoveTo)) return "无法移动到该位置";

            if (cmd.Action == ActionKind.Wait) return null;
            if (!unitById.TryGetValue(cmd.TargetId, out var t)) return "目标不存在";
            if (t.Side == u.Side) return "目标不是敌人";

            if (cmd.Action == ActionKind.Attack)
            {
                if (!t.InFight) return "目标无法被攻击";
                if (!InRange(u.Weapon, cmd.MoveTo, t.Pos)) return "目标不在射程内";
                return null;
            }

            if (!t.Capturable) return "目标不能被扛起";
            if (cmd.MoveTo.Manhattan(t.Pos) != 1) return "必须与目标相邻";
            if (u.IsCarrying) return "已经扛着一个俘虏";
            if (u.Stats.Bld < t.Stats.Bld) return "体格不足,扛不动";
            return null;
        }

        // ---------- Combat ----------

        public static bool InRange(Weapon w, GridPos from, GridPos to)
        {
            int d = from.Manhattan(to);
            return d >= w.MinRange && d <= w.MaxRange;
        }

        public AttackForecast Forecast(Unit attacker, GridPos from, Unit defender, GridPos defenderPos)
        {
            var a = EffectiveStats(attacker);
            var d = EffectiveStats(defender);
            var terrain = Config.Terrain(Map.Get(defenderPos));

            int hit = attacker.Weapon.Hit + a.Skl * Config.HitPerSkill + AuraLeadership(attacker, from) / Config.AuraHitDivisor;
            if (attacker.Morale < Config.WaverThreshold) hit -= Config.WaverHitPenalty;
            int avoid = d.Spd * Config.AvoidPerSpeed + terrain.Avoid + AuraLeadership(defender, defenderPos) / Config.AuraAvoidDivisor;
            if (defender.Morale < Config.WaverThreshold) avoid -= Config.WaverAvoidPenalty;

            return new AttackForecast
            {
                Hit = Math.Max(0, Math.Min(100, hit - avoid)),
                Damage = Math.Max(0, a.Str + attacker.Weapon.Might - d.Def - terrain.Defense),
                Strikes = a.Spd - d.Spd >= Config.DoubleAttackSpeedGap ? 2 : 1,
            };
        }

        public bool CanCounter(Unit defender, GridPos defenderPos, GridPos attackerPos) =>
            defender.Status == UnitStatus.Active && InRange(defender.Weapon, defenderPos, attackerPos);

        private void ResolveAttack(Unit att, Unit def)
        {
            var fa = Forecast(att, att.Pos, def, def.Pos);
            bool counter = CanCounter(def, def.Pos, att.Pos);
            var fd = counter ? Forecast(def, def.Pos, att, att.Pos) : default;
            Log.Add($"{att.Name} 攻击 {def.Name}(命中 {fa.Hit}% 伤害 {fa.Damage}×{fa.Strikes})");

            Strike(att, def, fa);
            if (counter && def.Status == UnitStatus.Active && att.Status == UnitStatus.Active) Strike(def, att, fd);
            if (fa.Strikes == 2 && att.Status == UnitStatus.Active && def.InFight) Strike(att, def, fa);
            if (counter && fd.Strikes == 2 && def.Status == UnitStatus.Active && att.Status == UnitStatus.Active) Strike(def, att, fd);
        }

        private void Strike(Unit src, Unit dst, AttackForecast f)
        {
            if (!Rng.Chance(f.Hit))
            {
                Log.Add($"  {src.Name} 未命中");
                return;
            }
            int dmg = Math.Min(f.Damage, dst.Hp);
            dst.Hp -= dmg;
            Log.Add($"  {src.Name} 造成 {dmg} 伤害,{dst.Name} 剩余 HP {dst.Hp}");
            if (dst.Hp <= 0)
            {
                Incapacitate(dst, src);
                return;
            }
            if (dmg > 0) LoseMorale(dst, dmg * Config.MoraleLossFullHpDamage / Math.Max(1, dst.Stats.MaxHp));
        }

        private void Incapacitate(Unit dst, Unit src)
        {
            if (src.Weapon.NonLethal)
            {
                dst.Status = UnitStatus.Stunned;
                if (dst.IsCarrying) DropCaptive(dst);
                Log.Add($"  {dst.Name} 被打晕");
            }
            else
            {
                dst.Status = UnitStatus.Downed;
                occupant[Map.Index(dst.Pos)] = -1;
                if (dst.IsCarrying) DropCaptive(dst);
                Log.Add($"  {dst.Name} 倒下");
            }

            if (src.InFight) src.Morale = Math.Min(Config.MoraleMax, src.Morale + Config.MoraleGainOnDefeat);

            var corps = corpsById[dst.CorpsId];
            bool wasLeader = corps.LeaderId == dst.Id;
            if (wasLeader) Log.Add($"  {corps.Name} 的兵团长倒下,全兵团士气大跌");
            foreach (var m in Members(corps))
            {
                if (m == dst || !m.InFight) continue;
                if (wasLeader) LoseMorale(m, Config.MoraleLossLeaderDown);
                else if (m.Pos.Manhattan(dst.Pos) <= Config.AllyDownRadius) LoseMorale(m, Config.MoraleLossAllyDown);
            }
        }

        // ---------- Morale & aura (D11) ----------

        // Leadership of the corps leader if `u` standing at `at` is inside the leader's aura, else 0.
        public int AuraLeadership(Unit u, GridPos at)
        {
            var corps = corpsById[u.CorpsId];
            if (corps.LeaderId < 0) return 0;
            var leader = unitById[corps.LeaderId];
            if (leader.Status != UnitStatus.Active) return 0;
            int radius = Config.AuraBaseRadius + leader.Stats.Ldr / Config.AuraLeadershipPerRadius;
            var leaderPos = leader.Id == u.Id ? at : leader.Pos;
            return at.Manhattan(leaderPos) <= radius ? leader.Stats.Ldr : 0;
        }

        private void LoseMorale(Unit u, int amount)
        {
            if (!u.InFight || amount <= 0) return;
            int ldr = AuraLeadership(u, u.Pos);
            int reduction = Math.Min(Config.AuraMoraleLossReductionMax, ldr * Config.AuraMoraleLossReductionPerLeadership);
            amount = amount * (100 - reduction) / 100;
            u.Morale = Math.Max(0, u.Morale - amount);
            if (u.Status == UnitStatus.Active && u.Morale <= 0)
            {
                u.Status = UnitStatus.Routed;
                if (u.IsCarrying) DropCaptive(u);
                Log.Add($"  {u.Name} 士气崩溃,开始溃逃");
            }
        }

        // ---------- Prisoners (D21) ----------

        private void PickUp(Unit carrier, Unit captive)
        {
            occupant[Map.Index(captive.Pos)] = -1;
            captive.StatusBeforeCarry = captive.Status;
            captive.Status = UnitStatus.Carried;
            captive.CarriedById = carrier.Id;
            captive.Pos = carrier.Pos;
            carrier.CarryingId = captive.Id;
            Log.Add($"{carrier.Name} 扛起俘虏 {captive.Name}");
        }

        private void DropCaptive(Unit carrier)
        {
            var captive = unitById[carrier.CarryingId];
            carrier.CarryingId = -1;
            captive.CarriedById = -1;
            captive.Status = captive.StatusBeforeCarry;
            captive.Pos = FindFreeTileNear(carrier.Pos);
            occupant[Map.Index(captive.Pos)] = captive.Id;
            Log.Add($"  俘虏 {captive.Name} 掉在地上");
        }

        private GridPos FindFreeTileNear(GridPos start)
        {
            var seen = new HashSet<GridPos> { start };
            var queue = new Queue<GridPos>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (occupant[Map.Index(p)] < 0) return p;
                foreach (var dir in GridPos.Directions)
                {
                    var n = p + dir;
                    if (!Map.InBounds(n) || !Config.Terrain(Map.Get(n)).Passable || !seen.Add(n)) continue;
                    queue.Enqueue(n);
                }
            }
            throw new InvalidOperationException("地图上没有空位放置俘虏");
        }

        // ---------- Outcome & score ----------

        private void CheckOutcome()
        {
            if (Outcome != BattleOutcome.Ongoing) return;
            bool playerIn = false, enemyIn = false;
            foreach (var u in Units)
            {
                if (!u.InFight) continue;
                if (u.Side == Side.Player) playerIn = true;
                else enemyIn = true;
            }
            if (!playerIn)
            {
                Outcome = BattleOutcome.PlayerLost;
                Log.Add("我方全军覆没,战斗失败");
            }
            else if (!enemyIn)
            {
                Outcome = BattleOutcome.PlayerWon;
                Log.Add("敌方已无可战斗单位,我方胜利");
            }
        }

        public MissionScore Score(Side side)
        {
            int deployed = 0, lost = 0;
            foreach (var u in Units)
            {
                if (u.Side != side) continue;
                deployed++;
                if (u.Status == UnitStatus.Downed || u.Status == UnitStatus.Stunned ||
                    u.Status == UnitStatus.Surrendered || u.Status == UnitStatus.Carried)
                    lost++;
            }
            int speed = Math.Max(0, Math.Min(100, 100 - (Round - 1) * 100 / TurnLimit));
            int casualty = deployed == 0 ? 100 : 100 - lost * 100 / deployed;
            int weights = Config.SpeedScoreWeight + Config.CasualtyScoreWeight;
            return new MissionScore
            {
                Speed = speed,
                Casualty = casualty,
                Total = (speed * Config.SpeedScoreWeight + casualty * Config.CasualtyScoreWeight) / weights,
            };
        }

        // ---------- AI-driven play ----------

        public void RunAiActivation()
        {
            var corps = ActiveCorps;
            if (corps == null) return;
            CorpsAi.Act(this, corps);
            EndActivation();
        }

        // Plays the battle until it ends; every corps must be AI-controlled (levels 4/5).
        public BattleOutcome RunToEnd(int maxActivations = 100000)
        {
            if (orderIndex < 0) Start();
            for (int i = 0; i < maxActivations && Outcome == BattleOutcome.Ongoing; i++)
            {
                if (ActiveCorps.Level == CommandLevel.Manual)
                    throw new InvalidOperationException($"{ActiveCorps.Name} 是手操兵团,无法自动运行");
                RunAiActivation();
            }
            return Outcome;
        }
    }
}
