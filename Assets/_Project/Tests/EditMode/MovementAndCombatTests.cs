using NUnit.Framework;
using Srpg.Core.Combat;
using static Srpg.Core.Tests.TestKit;

namespace Srpg.Core.Tests
{
    public class MovementAndCombatTests
    {
        [Test]
        public void ForestCostsTwoMovement()
        {
            var map = BattleMap.FromRows(".T...", ".....");
            var p = U(1, Side.Player, 1, 0, 0, Stats(mov: 3));
            var e = U(2, Side.Enemy, 2, 4, 1);
            var b = Make(map, new[] { p, e }, new[] { C(1, Side.Player, 1, p) }, new[] { C(2, Side.Enemy, 2, e) });

            var reach = b.ReachableTiles(p);
            Assert.AreEqual(2, reach[new GridPos(1, 0)]);
            Assert.AreEqual(3, reach[new GridPos(2, 0)]);
            Assert.IsFalse(reach.ContainsKey(new GridPos(3, 0)));
        }

        [Test]
        public void EnemiesBlockButAlliesCanBePassed()
        {
            var map = BattleMap.FromRows(".....");
            var p = U(1, Side.Player, 1, 0, 0);
            var ally = U(2, Side.Player, 1, 1, 0);
            var e = U(3, Side.Enemy, 2, 3, 0);
            var b = Make(map, new[] { p, ally, e }, new[] { C(1, Side.Player, 1, p, ally) }, new[] { C(2, Side.Enemy, 3, e) });

            var reach = b.ReachableTiles(p);
            Assert.IsTrue(reach.ContainsKey(new GridPos(2, 0)), "可以穿过友军");
            Assert.IsFalse(reach.ContainsKey(new GridPos(4, 0)), "敌人挡路");
            Assert.IsFalse(b.Destinations(p).Contains(new GridPos(1, 0)), "不能停在友军格子上");
        }

        [Test]
        public void DamageSubtractsDefenseAndTerrain()
        {
            var map = BattleMap.FromRows("..T");
            var p = U(1, Side.Player, 1, 0, 0);
            var e = U(2, Side.Enemy, 2, 2, 0);
            var b = Make(map, new[] { p, e }, new[] { C(1, Side.Player, 1, p) }, new[] { C(2, Side.Enemy, 2, e) });

            // 力量 6 + 威力 5 − 防御 3 − 森林 1
            Assert.AreEqual(7, b.Forecast(p, new GridPos(1, 0), e, e.Pos).Damage);
        }

        [Test]
        public void FasterUnitStrikesTwice()
        {
            var map = Open(3, 1);
            var fast = U(1, Side.Player, 1, 0, 0, Stats(spd: 9));
            var slow = U(2, Side.Enemy, 2, 1, 0, Stats(spd: 5));
            var b = Make(map, new[] { fast, slow }, new[] { C(1, Side.Player, 1, fast) }, new[] { C(2, Side.Enemy, 2, slow) });

            Assert.AreEqual(2, b.Forecast(fast, fast.Pos, slow, slow.Pos).Strikes);
            Assert.AreEqual(1, b.Forecast(slow, slow.Pos, fast, fast.Pos).Strikes);
        }

        [Test]
        public void LethalWeaponDownsAndFreesTile()
        {
            var map = Open(4, 1);
            var p = U(1, Side.Player, 1, 0, 0, Stats(str: 20));
            var e = U(2, Side.Enemy, 2, 1, 0);
            var e2 = U(3, Side.Enemy, 2, 3, 0);
            var b = Make(map, new[] { p, e, e2 }, new[] { C(1, Side.Player, 1, p) }, new[] { C(2, Side.Enemy, 3, e, e2) });
            b.Start();

            Act(b, UnitCommand.Attack(p, p.Pos, e));

            Assert.AreEqual(UnitStatus.Downed, e.Status);
            Assert.IsNull(b.UnitAt(new GridPos(1, 0)));
        }

        [Test]
        public void NonLethalWeaponStunsAndTargetStaysOnMap()
        {
            var map = Open(4, 1);
            var p = U(1, Side.Player, 1, 0, 0, Stats(str: 20), Club());
            var e = U(2, Side.Enemy, 2, 1, 0);
            var e2 = U(3, Side.Enemy, 2, 3, 0);
            var b = Make(map, new[] { p, e, e2 }, new[] { C(1, Side.Player, 1, p) }, new[] { C(2, Side.Enemy, 3, e, e2) });
            b.Start();

            Act(b, UnitCommand.Attack(p, p.Pos, e));

            Assert.AreEqual(UnitStatus.Stunned, e.Status);
            Assert.AreSame(e, b.UnitAt(new GridPos(1, 0)));
        }

        [Test]
        public void DefenderCountersOnlyWithinItsRange()
        {
            var map = Open(4, 1);
            var p = U(1, Side.Player, 1, 0, 0, Stats(hp: 40));
            var archer = U(2, Side.Enemy, 2, 1, 0, Stats(hp: 40), Bow());
            var b = Make(map, new[] { p, archer }, new[] { C(1, Side.Player, 1, p) }, new[] { C(2, Side.Enemy, 2, archer) });
            b.Start();

            Act(b, UnitCommand.Attack(p, p.Pos, archer));

            Assert.AreEqual(40, p.Hp, "弓手无法反击相邻的敌人");
            Assert.Less(archer.Hp, 40);
        }

        [Test]
        public void CounterattackHitsBack()
        {
            var map = Open(3, 1);
            var p = U(1, Side.Player, 1, 0, 0, Stats(hp: 40));
            var e = U(2, Side.Enemy, 2, 1, 0, Stats(hp: 40));
            var b = Make(map, new[] { p, e }, new[] { C(1, Side.Player, 1, p) }, new[] { C(2, Side.Enemy, 2, e) });
            b.Start();

            Act(b, UnitCommand.Attack(p, p.Pos, e));

            Assert.AreEqual(40 - 8, p.Hp);
            Assert.AreEqual(40 - 8, e.Hp);
        }
    }
}
