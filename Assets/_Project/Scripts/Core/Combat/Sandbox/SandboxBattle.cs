using System.Collections.Generic;

namespace Srpg.Core.Combat.Sandbox
{
    // Development test field shared by the Unity gray-box scene and Tools/BattleSim. Not game content.
    public static class SandboxBattle
    {
        public const int FortCorpsId = 203;

        private static readonly string[] PlayerCorpsNames = { "赤狼", "青鹰", "黑熊", "白鹿" };
        private static readonly string[] EnemyCorpsNames = { "北门守备", "河岸巡逻", "山道伏兵", "要塞卫队" };

        // 40×24 field: river with two bridges, mountains to the north, a fort to the east. 4 corps × 12 per side.
        public static Battle Create(ulong seed, BattleConfig config = null)
        {
            var rng = new Rng(seed);
            const int w = 40, h = 24;
            var map = new BattleMap(w, h);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                TerrainType t = TerrainType.Plain;
                bool river = x == 19 || x == 20;
                bool bridge = y == 5 || y == 6 || y == 17 || y == 18;
                if (river && !bridge) t = TerrainType.Water;
                else if (!river && x > 13 && x < 26 && y < 3) t = TerrainType.Mountain;
                else if (x >= 32 && x <= 34 && y >= 10 && y <= 13) t = TerrainType.Fort;
                else if (x > 4 && x < 36 && !river && rng.Chance(14)) t = TerrainType.Forest;
                map.Set(new GridPos(x, y), t);
            }

            var units = new List<Unit>();
            var player = new List<Corps>();
            var enemy = new List<Corps>();
            int id = 1;
            for (int c = 0; c < 4; c++)
            {
                player.Add(MakeCorps(Side.Player, 100 + c, PlayerCorpsNames[c], 1, c * 6 + 1, rng, units, ref id));
                int enemyY = c == 3 ? 10 : c * 6 + 1;
                int enemyX = c == 3 ? 33 : 35;
                enemy.Add(MakeCorps(Side.Enemy, 200 + c, EnemyCorpsNames[c], enemyX, enemyY, rng, units, ref id));
            }
            enemy[3].Level = CommandLevel.Coarse;
            enemy[3].Order = CorpsOrder.HoldAt(new GridPos(33, 11), 3);

            return new Battle(map, config ?? new BattleConfig(), seed, 25, units, player, enemy);
        }

        private static Corps MakeCorps(Side side, int corpsId, string name, int x0, int y0, Rng rng,
            List<Unit> units, ref int id)
        {
            var corps = new Corps { Id = corpsId, Name = name, Side = side };
            for (int i = 0; i < 12; i++)
            {
                var stats = new UnitStats
                {
                    MaxHp = 18 + rng.Range(0, 7),
                    Str = 5 + rng.Range(0, 4),
                    Skl = 4 + rng.Range(0, 5),
                    Spd = 4 + rng.Range(0, 5),
                    Def = 2 + rng.Range(0, 4),
                    Bld = 5 + rng.Range(0, 6),
                    Ldr = i == 0 ? 10 + rng.Range(0, 11) : rng.Range(0, 6),
                    Mov = 5,
                };
                var u = new Unit
                {
                    Id = id++,
                    Name = $"{name}{i + 1}",
                    Side = side,
                    CorpsId = corpsId,
                    Stats = stats,
                    Weapon = MakeWeapon(side, i),
                    Pos = new GridPos(x0 + i % 4, y0 + i / 4),
                };
                units.Add(u);
                corps.Members.Add(u.Id);
            }
            corps.LeaderId = corps.Members[0];
            return corps;
        }

        // Every 4th unit is an archer; each player corps also carries one club for trying captures.
        private static Weapon MakeWeapon(Side side, int index)
        {
            if (index % 4 == 3) return new Weapon { Name = "弓", Might = 5, Hit = 85, MinRange = 2, MaxRange = 2 };
            if (side == Side.Player && index == 2) return new Weapon { Name = "棍", Might = 4, Hit = 90, NonLethal = true };
            return new Weapon { Name = "剑", Might = 5, Hit = 90 };
        }
    }
}
