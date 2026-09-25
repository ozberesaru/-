using System.Collections.Generic;

namespace Srpg.Core.Combat
{
    public static class Pathfinding
    {
        // Tiles the unit can pass through within `move`, with their cost. Allies can be passed, enemies block.
        public static Dictionary<GridPos, int> Reachable(Battle battle, Unit unit, int move)
        {
            var map = battle.Map;
            var cost = new Dictionary<GridPos, int> { [unit.Pos] = 0 };
            var heap = new MinHeap<GridPos>();
            heap.Push(0, unit.Pos);
            while (heap.Count > 0)
            {
                var (c, p) = heap.Pop();
                if (c > cost[p]) continue;
                foreach (var dir in GridPos.Directions)
                {
                    var n = p + dir;
                    if (!map.InBounds(n)) continue;
                    var terrain = battle.Config.Terrain(map.Get(n));
                    if (!terrain.Passable) continue;
                    var occupant = battle.UnitAt(n);
                    if (occupant != null && occupant.Side != unit.Side) continue;
                    int nc = c + terrain.MoveCost;
                    if (nc > move) continue;
                    if (cost.TryGetValue(n, out int old) && old <= nc) continue;
                    cost[n] = nc;
                    heap.Push(nc, n);
                }
            }
            return cost;
        }

        // Terrain-only travel cost from the nearest source to every tile; int.MaxValue if unreachable.
        public static int[] DistanceField(BattleMap map, BattleConfig config, IEnumerable<GridPos> sources)
        {
            var field = new int[map.Width * map.Height];
            for (int i = 0; i < field.Length; i++) field[i] = int.MaxValue;
            var heap = new MinHeap<GridPos>();
            foreach (var s in sources)
            {
                if (!map.InBounds(s)) continue;
                field[map.Index(s)] = 0;
                heap.Push(0, s);
            }
            while (heap.Count > 0)
            {
                var (c, p) = heap.Pop();
                if (c > field[map.Index(p)]) continue;
                foreach (var dir in GridPos.Directions)
                {
                    var n = p + dir;
                    if (!map.InBounds(n)) continue;
                    var terrain = config.Terrain(map.Get(n));
                    if (!terrain.Passable) continue;
                    int nc = c + terrain.MoveCost;
                    int idx = map.Index(n);
                    if (nc >= field[idx]) continue;
                    field[idx] = nc;
                    heap.Push(nc, n);
                }
            }
            return field;
        }
    }
}
