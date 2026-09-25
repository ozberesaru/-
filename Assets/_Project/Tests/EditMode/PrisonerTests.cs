using System.Linq;
using NUnit.Framework;
using Srpg.Core.Combat;
using static Srpg.Core.Tests.TestKit;

namespace Srpg.Core.Tests
{
    public class PrisonerTests
    {
        // Player: stunner (0,0), carrier (0,1). Enemy: target (1,0), guard (5,1).
        private static Battle Setup(int carrierBld, int targetBld, out Unit carrier, out Unit target, out Unit guard)
        {
            var map = Open(10, 2);
            var stunner = U(1, Side.Player, 1, 0, 0, Stats(str: 30), Club());
            carrier = U(2, Side.Player, 1, 0, 1, Stats(bld: carrierBld));
            target = U(3, Side.Enemy, 2, 1, 0, Stats(bld: targetBld));
            guard = U(4, Side.Enemy, 2, 5, 1, Stats(str: 40));
            var b = Make(map, new[] { stunner, carrier, target, guard },
                new[] { C(1, Side.Player, 1, stunner, carrier) }, new[] { C(2, Side.Enemy, 4, target, guard) });
            b.Start();
            Act(b, UnitCommand.Attack(stunner, stunner.Pos, target));
            Assert.AreEqual(UnitStatus.Stunned, target.Status);
            return b;
        }

        [Test]
        public void CannotCarrySomeoneWithBiggerBuild()
        {
            var b = Setup(5, 8, out var carrier, out var target, out _);

            bool ok = b.TryExecute(UnitCommand.PickUp(carrier, new GridPos(1, 1), target), out string error);

            Assert.IsFalse(ok);
            StringAssert.Contains("体格", error);
        }

        [Test]
        public void BiggerBuildGapMeansSmallerPenalty()
        {
            var b = Setup(12, 6, out var strong, out var target, out _);
            Act(b, UnitCommand.PickUp(strong, new GridPos(1, 1), target));
            var s = b.EffectiveStats(strong);
            Assert.AreEqual(4, s.Mov, "负担 50%:移动 −1");
            Assert.AreEqual(4, s.Str, "负担 50%:力量 ×75%");

            var b2 = Setup(6, 6, out var even, out var target2, out _);
            Act(b2, UnitCommand.PickUp(even, new GridPos(1, 1), target2));
            var s2 = b2.EffectiveStats(even);
            Assert.AreEqual(3, s2.Mov, "负担 100%:移动 −2");
            Assert.AreEqual(3, s2.Str, "负担 100%:力量 ×50%");
        }

        [Test]
        public void CaptiveMovesWithCarrierAndDropsWhenCarrierFalls()
        {
            var b = Setup(8, 6, out var carrier, out var target, out var guard);
            Act(b, UnitCommand.PickUp(carrier, new GridPos(1, 1), target));
            Assert.AreEqual(UnitStatus.Carried, target.Status);
            Assert.IsNull(b.UnitAt(new GridPos(1, 0)), "被扛起的俘虏离开原来的格子");

            b.EndActivation();
            Act(b, UnitCommand.Attack(guard, new GridPos(2, 1), carrier));

            Assert.AreEqual(UnitStatus.Downed, carrier.Status);
            Assert.AreEqual(UnitStatus.Stunned, target.Status);
            Assert.AreEqual(new GridPos(1, 1), target.Pos);
            Assert.AreSame(target, b.UnitAt(new GridPos(1, 1)));
        }

        [Test]
        public void CaptiveStillCarriedAtVictoryIsCaptured()
        {
            var b = Setup(8, 6, out var carrier, out var target, out var guard);
            Act(b, UnitCommand.PickUp(carrier, new GridPos(1, 1), target));
            guard.Stats.MaxHp = 1;
            guard.Hp = 1;
            b.EndActivation();
            b.EndActivation();

            var stunner = b.GetUnit(1);
            var reach = b.Destinations(stunner);
            var spot = reach.First(p => p.Manhattan(guard.Pos) == 1);
            Act(b, UnitCommand.Attack(stunner, spot, guard));

            Assert.AreEqual(BattleOutcome.PlayerWon, b.Outcome);
            CollectionAssert.Contains(b.CapturedBy(Side.Player).ToList(), target);
        }
    }
}
