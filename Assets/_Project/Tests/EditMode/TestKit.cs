using System.Collections.Generic;
using Srpg.Core.Combat;

namespace Srpg.Core.Tests
{
    internal static class TestKit
    {
        public const int SureHit = 300;

        public static Weapon Sword(int hit = SureHit) => new Weapon { Name = "剑", Might = 5, Hit = hit };
        public static Weapon Club(int hit = SureHit) => new Weapon { Name = "棍", Might = 4, Hit = hit, NonLethal = true };
        public static Weapon Bow(int hit = SureHit) => new Weapon { Name = "弓", Might = 5, Hit = hit, MinRange = 2, MaxRange = 2 };

        public static UnitStats Stats(int hp = 20, int str = 6, int skl = 5, int spd = 5, int def = 3,
            int bld = 6, int ldr = 0, int mov = 5) =>
            new UnitStats { MaxHp = hp, Str = str, Skl = skl, Spd = spd, Def = def, Bld = bld, Ldr = ldr, Mov = mov };

        public static Unit U(int id, Side side, int corpsId, int x, int y, UnitStats? stats = null, Weapon weapon = null) =>
            new Unit
            {
                Id = id,
                Name = (side == Side.Player ? "我" : "敌") + id,
                Side = side,
                CorpsId = corpsId,
                Pos = new GridPos(x, y),
                Stats = stats ?? Stats(),
                Weapon = weapon ?? Sword(),
            };

        public static Corps C(int id, Side side, int leaderId, params Unit[] members)
        {
            var c = new Corps { Id = id, Name = (side == Side.Player ? "我方兵团" : "敌方兵团") + id, Side = side, LeaderId = leaderId };
            foreach (var m in members) c.Members.Add(m.Id);
            return c;
        }

        public static Battle Make(BattleMap map, IEnumerable<Unit> units, Corps[] player, Corps[] enemy,
            int turnLimit = 20, ulong seed = 1, BattleConfig config = null)
        {
            return new Battle(map, config ?? new BattleConfig(), seed, turnLimit, units, player, enemy);
        }

        public static BattleMap Open(int width, int height)
        {
            var rows = new string[height];
            for (int y = 0; y < height; y++) rows[y] = new string('.', width);
            return BattleMap.FromRows(rows);
        }

        public static void Act(Battle b, UnitCommand cmd)
        {
            if (!b.TryExecute(cmd, out string error)) throw new System.InvalidOperationException(error);
        }
    }
}
