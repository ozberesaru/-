using System.Linq;
using NUnit.Framework;
using Srpg.Core.Combat;
using Srpg.Core.Combat.Sandbox;
using static Srpg.Core.Tests.TestKit;

namespace Srpg.Core.Tests
{
    public class QueryAndSandboxTests
    {
        [Test]
        public void AttackTargetsRespectWeaponRange()
        {
            var map = Open(8, 1);
            var archer = U(1, Side.Player, 1, 0, 0, weapon: Bow());
            var near = U(2, Side.Enemy, 2, 3, 0);
            var far = U(3, Side.Enemy, 2, 5, 0);
            var b = Make(map, new[] { archer, near, far }, new[] { C(1, Side.Player, 1, archer) }, new[] { C(2, Side.Enemy, 2, near, far) });

            CollectionAssert.AreEquivalent(new[] { near }, b.AttackTargetsFrom(archer, new GridPos(1, 0)));
            CollectionAssert.AreEquivalent(new[] { far }, b.AttackTargetsFrom(archer, new GridPos(3, 0)).Where(e => e == far));
            CollectionAssert.IsEmpty(b.AttackTargetsFrom(archer, new GridPos(2, 0)).Where(e => e == near));
        }

        [Test]
        public void PickUpTargetsNeedAdjacencyAndBuild()
        {
            var map = Open(6, 2);
            var stunner = U(1, Side.Player, 1, 0, 0, Stats(str: 30), Club());
            var small = U(2, Side.Player, 1, 0, 1, Stats(bld: 4));
            var big = U(3, Side.Player, 1, 3, 1, Stats(bld: 9));
            var target = U(4, Side.Enemy, 2, 1, 0, Stats(bld: 6));
            var other = U(5, Side.Enemy, 2, 5, 0);
            var b = Make(map, new[] { stunner, small, big, target, other },
                new[] { C(1, Side.Player, 1, stunner, small, big) }, new[] { C(2, Side.Enemy, 5, target, other) });
            b.Start();
            Act(b, UnitCommand.Attack(stunner, stunner.Pos, target));

            CollectionAssert.IsEmpty(b.PickUpTargetsFrom(small, new GridPos(1, 1)), "体格不足");
            CollectionAssert.AreEquivalent(new[] { target }, b.PickUpTargetsFrom(big, new GridPos(1, 1)));
            CollectionAssert.IsEmpty(b.PickUpTargetsFrom(big, new GridPos(2, 1)), "不相邻");
        }

        [Test]
        public void SandboxBattleIsValidAndPlaysToTheEnd()
        {
            var b = SandboxBattle.Create(3);

            Assert.AreEqual(48, b.Units.Count(u => u.Side == Side.Player));
            Assert.AreEqual(48, b.Units.Count(u => u.Side == Side.Enemy));
            Assert.AreNotEqual(BattleOutcome.Ongoing, b.RunToEnd());
        }
    }
}
