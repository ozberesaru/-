using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Srpg.Core.Combat;
using static Srpg.Core.Tests.TestKit;

namespace Srpg.Core.Tests
{
    public class CorpsAiTests
    {
        [Test]
        public void AutoCorpsAttacksReachableEnemy()
        {
            var map = Open(8, 3);
            var p = U(1, Side.Player, 1, 0, 1, Stats(str: 30));
            var e = U(2, Side.Enemy, 2, 4, 1);
            var b = Make(map, new[] { p, e }, new[] { C(1, Side.Player, 1, p) }, new[] { C(2, Side.Enemy, 2, e) });
            b.Start();

            b.RunAiActivation();

            Assert.AreEqual(UnitStatus.Downed, e.Status);
            Assert.AreEqual(BattleOutcome.PlayerWon, b.Outcome);
        }

        [Test]
        public void HoldOrderKeepsCorpsAroundThePoint()
        {
            var map = Open(20, 9);
            var members = new List<Unit>();
            for (int i = 0; i < 4; i++) members.Add(U(1 + i, Side.Player, 1, 2, 2 + i));
            var e = U(10, Side.Enemy, 2, 19, 4);
            var corps = C(1, Side.Player, 1, members.ToArray());
            corps.Level = CommandLevel.Coarse;
            corps.Order = CorpsOrder.HoldAt(new GridPos(4, 4), 2);
            var b = Make(map, members.Append(e), new[] { corps }, new[] { C(2, Side.Enemy, 10, e) });
            b.Start();

            b.RunAiActivation();

            foreach (var m in members)
                Assert.LessOrEqual(m.Pos.Manhattan(new GridPos(4, 4)), 2, $"{m} 应留在固守点附近");
        }

        [Test]
        public void RetreatOrderNeverAttacks()
        {
            var map = Open(10, 3);
            var p = U(1, Side.Player, 1, 5, 1);
            var e = U(2, Side.Enemy, 2, 6, 1, Stats(hp: 40));
            var corps = C(1, Side.Player, 1, p);
            corps.Level = CommandLevel.Coarse;
            corps.Order = CorpsOrder.RetreatTo(new GridPos(0, 1));
            var b = Make(map, new[] { p, e }, new[] { corps }, new[] { C(2, Side.Enemy, 2, e) });
            b.Start();

            b.RunAiActivation();

            Assert.AreEqual(40, e.Hp);
            Assert.Less(p.Pos.X, 5);
        }

        [Test]
        public void LargeAutoBattleFinishesAndIsDeterministic()
        {
            var first = RunLargeBattle(seed: 42);
            var second = RunLargeBattle(seed: 42);

            Assert.AreNotEqual(BattleOutcome.Ongoing, first.Outcome);
            Assert.AreEqual(first.Outcome, second.Outcome);
            Assert.AreEqual(first.Round, second.Round);
            CollectionAssert.AreEqual(first.Log, second.Log);
        }

        [Test]
        public void StrongerArmyUsuallyWins()
        {
            int wins = 0;
            for (ulong seed = 1; seed <= 5; seed++)
                if (RunLargeBattle(seed, playerBonus: 3).Outcome == BattleOutcome.PlayerWon) wins++;
            Assert.GreaterOrEqual(wins, 4);
        }

        // 4 corps × 12 per side on an open field with forest patches.
        private static Battle RunLargeBattle(ulong seed, int playerBonus = 0)
        {
            var rng = new Rng(seed);
            var rows = new string[24];
            for (int y = 0; y < rows.Length; y++)
            {
                var chars = new char[32];
                for (int x = 0; x < chars.Length; x++)
                    chars[x] = x > 6 && x < 25 && rng.Chance(12) ? 'T' : '.';
                rows[y] = new string(chars);
            }
            var map = BattleMap.FromRows(rows);

            var units = new List<Unit>();
            var playerCorps = new List<Corps>();
            var enemyCorps = new List<Corps>();
            int id = 1;
            foreach (var side in new[] { Side.Player, Side.Enemy })
            {
                for (int c = 0; c < 4; c++)
                {
                    int corpsId = (side == Side.Player ? 100 : 200) + c;
                    var members = new List<Unit>();
                    for (int i = 0; i < 12; i++)
                    {
                        int x = side == Side.Player ? 1 + i % 3 : 30 - i % 3;
                        int y = c * 6 + i / 3;
                        int bonus = side == Side.Player ? playerBonus : 0;
                        var stats = Stats(hp: 18 + rng.Range(0, 6), str: 5 + rng.Range(0, 3) + bonus,
                            skl: 4 + rng.Range(0, 4), spd: 4 + rng.Range(0, 4), def: 2 + rng.Range(0, 3) + bonus,
                            ldr: i == 0 ? 15 : 0, mov: 5);
                        var weapon = i % 4 == 3 ? Bow(85) : Sword(85);
                        var u = U(id++, side, corpsId, x, y, stats, weapon);
                        members.Add(u);
                        units.Add(u);
                    }
                    var corps = C(corpsId, side, members[0].Id, members.ToArray());
                    (side == Side.Player ? playerCorps : enemyCorps).Add(corps);
                }
            }

            var b = Make(map, units, playerCorps.ToArray(), enemyCorps.ToArray(), turnLimit: 30, seed: seed);
            b.RunToEnd();
            return b;
        }
    }
}
