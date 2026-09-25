using System;
using System.Collections.Generic;

namespace Srpg.Core.Combat
{
    // Drives a corps at command levels 4 (coarse order) and 5 (fully autonomous).
    public static class CorpsAi
    {
        private const float MinAttackScore = 0.5f;

        public static void Act(Battle battle, Corps corps)
        {
            var order = corps.Level == CommandLevel.Auto ? CorpsOrder.AttackNearest() : corps.Order;
            var field = Pathfinding.DistanceField(battle.Map, battle.Config, ObjectiveTiles(battle, corps, order));

            var units = battle.ControllableUnits(corps);
            units.Sort((a, b) =>
            {
                int c = field[battle.Map.Index(a.Pos)].CompareTo(field[battle.Map.Index(b.Pos)]);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });

            foreach (var u in units)
            {
                if (battle.Outcome != BattleOutcome.Ongoing) return;
                if (u.Status != UnitStatus.Active || u.HasActed) continue;
                var cmd = Choose(battle, u, order, field);
                if (!battle.TryExecute(cmd, out _)) battle.TryExecute(UnitCommand.Wait(u, u.Pos), out _);
            }
        }

        private static List<GridPos> ObjectiveTiles(Battle battle, Corps corps, CorpsOrder order)
        {
            var tiles = new List<GridPos>();
            if (order.Type == OrderType.Follow && order.FollowCorpsId >= 0)
            {
                foreach (var u in battle.Members(battle.GetCorps(order.FollowCorpsId)))
                    if (u.InFight) tiles.Add(u.Pos);
                if (tiles.Count > 0) return tiles;
            }
            else if (order.HasTarget)
            {
                tiles.Add(order.Target);
                return tiles;
            }
            foreach (var e in battle.Units)
                if (e.Side != corps.Side && e.InFight) tiles.Add(e.Pos);
            return tiles;
        }

        private static bool MayAttack(CorpsOrder order, Unit enemy)
        {
            switch (order.Type)
            {
                case OrderType.Retreat: return false;
                case OrderType.Hold: return enemy.Pos.Manhattan(order.Target) <= order.HoldRadius + 2;
                default: return true;
            }
        }

        private static UnitCommand Choose(Battle battle, Unit u, CorpsOrder order, int[] field)
        {
            var dests = battle.Destinations(u);
            if (order.Type == OrderType.Hold)
            {
                var inside = dests.FindAll(d => d.Manhattan(order.Target) <= order.HoldRadius);
                if (inside.Count > 0) dests = inside;
            }

            int move = battle.EffectiveStats(u).Mov;
            var enemies = new List<Unit>();
            foreach (var e in battle.Units)
                if (e.Side != u.Side && e.InFight && MayAttack(order, e) &&
                    u.Pos.Manhattan(e.Pos) <= move + u.Weapon.MaxRange)
                    enemies.Add(e);

            float bestScore = MinAttackScore;
            UnitCommand? bestAttack = null;
            foreach (var d in dests)
            {
                foreach (var e in enemies)
                {
                    if (!Battle.InRange(u.Weapon, d, e.Pos)) continue;
                    float score = AttackScore(battle, u, d, e);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestAttack = UnitCommand.Attack(u, d, e);
                    }
                }
            }
            if (bestAttack.HasValue) return bestAttack.Value;

            GridPos best = u.Pos;
            long bestKey = long.MaxValue;
            foreach (var d in dests)
            {
                long key = MoveKey(battle, order, field, d);
                if (key < bestKey)
                {
                    bestKey = key;
                    best = d;
                }
            }
            return UnitCommand.Wait(u, best);
        }

        private static float AttackScore(Battle battle, Unit u, GridPos from, Unit target)
        {
            var f = battle.Forecast(u, from, target, target.Pos);
            int total = f.Damage * f.Strikes;
            float score = f.Hit * Math.Min(total, target.Hp) / 100f;
            if (total >= target.Hp) score += f.Hit * 0.2f;
            if (battle.IsLeader(target)) score += 5f;

            if (battle.CanCounter(target, target.Pos, from))
            {
                var c = battle.Forecast(target, target.Pos, u, from);
                int counterTotal = c.Damage * c.Strikes;
                score -= 0.6f * c.Hit * Math.Min(counterTotal, u.Hp) / 100f;
                if (counterTotal >= u.Hp) score -= c.Hit * 0.2f;
            }

            score += battle.Config.Terrain(battle.Map.Get(from)).Avoid * 0.05f;
            return score;
        }

        // Lower is better: travel cost to the objective first, then prefer defensive terrain.
        private static long MoveKey(Battle battle, CorpsOrder order, int[] field, GridPos d)
        {
            long dist = field[battle.Map.Index(d)];
            if (order.Type == OrderType.Hold && dist != int.MaxValue) dist = Math.Max(0, dist - order.HoldRadius);
            int avoid = battle.Config.Terrain(battle.Map.Get(d)).Avoid;
            return dist * 1000 - avoid;
        }
    }
}
