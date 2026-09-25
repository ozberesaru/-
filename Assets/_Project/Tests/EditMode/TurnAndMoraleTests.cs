using NUnit.Framework;
using Srpg.Core.Combat;
using static Srpg.Core.Tests.TestKit;

namespace Srpg.Core.Tests
{
    public class TurnAndMoraleTests
    {
        [Test]
        public void CorpsAlternateAndExtraCorpsActLast()
        {
            var map = Open(10, 4);
            var p1 = U(1, Side.Player, 1, 0, 0);
            var p2 = U(2, Side.Player, 2, 0, 1);
            var p3 = U(3, Side.Player, 3, 0, 2);
            var e = U(10, Side.Enemy, 10, 9, 0);
            var b = Make(map, new[] { p1, p2, p3, e },
                new[] { C(1, Side.Player, 1, p1), C(2, Side.Player, 2, p2), C(3, Side.Player, 3, p3) },
                new[] { C(10, Side.Enemy, 10, e) });

            CollectionAssert.AreEqual(new[] { 1, 10, 2, 3 }, b.TurnOrder);
        }

        [Test]
        public void RoundAdvancesAfterEveryCorpsActedAndTimeRunsOut()
        {
            var map = Open(10, 1);
            var p = U(1, Side.Player, 1, 0, 0);
            var e = U(2, Side.Enemy, 2, 9, 0);
            var b = Make(map, new[] { p, e }, new[] { C(1, Side.Player, 1, p) }, new[] { C(2, Side.Enemy, 2, e) }, turnLimit: 2);
            b.Start();

            Assert.AreEqual(1, b.ActiveCorps.Id);
            b.EndActivation();
            Assert.AreEqual(2, b.ActiveCorps.Id);
            b.EndActivation();
            Assert.AreEqual(2, b.Round);
            Assert.AreEqual(1, b.ActiveCorps.Id);
            b.EndActivation();
            b.EndActivation();
            Assert.AreEqual(BattleOutcome.TimeUp, b.Outcome);
        }

        [Test]
        public void CorpsWithoutFightersIsSkipped()
        {
            var map = Open(10, 1);
            var p = U(1, Side.Player, 1, 0, 0, Stats(str: 30));
            var e1 = U(2, Side.Enemy, 2, 1, 0);
            var e2 = U(3, Side.Enemy, 3, 9, 0);
            var b = Make(map, new[] { p, e1, e2 }, new[] { C(1, Side.Player, 1, p) },
                new[] { C(2, Side.Enemy, 2, e1), C(3, Side.Enemy, 3, e2) });
            b.Start();

            Act(b, UnitCommand.Attack(p, p.Pos, e1));
            b.EndActivation();

            Assert.AreEqual(3, b.ActiveCorps.Id);
        }

        [Test]
        public void LeaderDownHitsWholeCorpsMorale()
        {
            var map = Open(10, 1);
            var p = U(1, Side.Player, 1, 0, 0, Stats(str: 30));
            var leader = U(2, Side.Enemy, 2, 1, 0, Stats(ldr: 20));
            var far = U(3, Side.Enemy, 2, 9, 0);
            var b = Make(map, new[] { p, leader, far }, new[] { C(1, Side.Player, 1, p) }, new[] { C(2, Side.Enemy, 2, leader, far) });
            b.Start();

            Act(b, UnitCommand.Attack(p, p.Pos, leader));

            Assert.AreEqual(b.Config.MoraleMax - b.Config.MoraleLossLeaderDown, far.Morale);
        }

        [Test]
        public void LeaderAuraSoftensMoraleLoss()
        {
            var map = Open(12, 3);
            var p = U(1, Side.Player, 1, 0, 0, Stats(str: 30));
            var victim = U(2, Side.Enemy, 2, 1, 0);
            var inAura = U(3, Side.Enemy, 2, 2, 0);
            var leader = U(4, Side.Enemy, 2, 4, 0, Stats(ldr: 20));
            var p2 = U(5, Side.Player, 1, 0, 2, Stats(str: 30));
            var victim2 = U(6, Side.Enemy, 3, 1, 2);
            var noLeader = U(7, Side.Enemy, 3, 2, 2);
            var b = Make(map, new[] { p, victim, inAura, leader, p2, victim2, noLeader },
                new[] { C(1, Side.Player, 1, p, p2) },
                new[] { C(2, Side.Enemy, 4, victim, inAura, leader), C(3, Side.Enemy, -1, victim2, noLeader) });
            b.Start();

            Act(b, UnitCommand.Attack(p, p.Pos, victim));
            Act(b, UnitCommand.Attack(p2, p2.Pos, victim2));

            int loss = b.Config.MoraleLossAllyDown;
            Assert.AreEqual(b.Config.MoraleMax - loss * 60 / 100, inAura.Morale, "领导力 20 的光环减少 40% 士气损失");
            Assert.AreEqual(b.Config.MoraleMax - loss, noLeader.Morale);
        }

        [Test]
        public void RoutedUnitFleesIsUncontrollableAndEscapesAtEdge()
        {
            var map = Open(8, 3);
            var p = U(1, Side.Player, 1, 1, 1, Stats(hp: 40));
            var e = U(2, Side.Enemy, 2, 2, 1);
            var b = Make(map, new[] { p, e }, new[] { C(1, Side.Player, 1, p) }, new[] { C(2, Side.Enemy, 2, e) });
            b.Start();
            e.Morale = 1;

            Act(b, UnitCommand.Attack(p, p.Pos, e));
            Assert.AreEqual(UnitStatus.Routed, e.Status);
            Assert.AreEqual(40, p.Hp, "溃逃的单位不反击");

            b.EndActivation();
            Assert.AreEqual(1, b.ActiveCorps.Id, "敌方兵团没有可控单位,直接轮到我方");
            Assert.Greater(e.Pos.Manhattan(p.Pos), 1);

            b.EndActivation();
            Assert.AreEqual(UnitStatus.Escaped, e.Status);
            Assert.AreEqual(BattleOutcome.PlayerWon, b.Outcome);
        }

        [Test]
        public void CorneredRoutedUnitSurrenders()
        {
            var map = BattleMap.FromRows("#####", "#...#", "#####");
            var e = U(2, Side.Enemy, 2, 1, 1);
            var p = U(1, Side.Player, 1, 2, 1);
            var b = Make(map, new[] { p, e }, new[] { C(1, Side.Player, 1, p) }, new[] { C(2, Side.Enemy, 2, e) });
            b.Start();
            e.Morale = 1;

            Act(b, UnitCommand.Attack(p, p.Pos, e));
            b.EndActivation();

            Assert.AreEqual(UnitStatus.Surrendered, e.Status);
            Assert.AreEqual(BattleOutcome.PlayerWon, b.Outcome);
        }

        [Test]
        public void RoutedUnitRalliesWhenMoraleRecovers()
        {
            var map = Open(8, 3);
            var p = U(1, Side.Player, 1, 1, 1, Stats(hp: 40));
            var e = U(2, Side.Enemy, 2, 2, 1);
            var b = Make(map, new[] { p, e }, new[] { C(1, Side.Player, 1, p) }, new[] { C(2, Side.Enemy, 2, e) });
            b.Start();
            e.Morale = 1;
            Act(b, UnitCommand.Attack(p, p.Pos, e));

            e.Morale = 25;
            b.EndActivation();

            Assert.AreEqual(UnitStatus.Active, e.Status);
            Assert.AreEqual(2, b.ActiveCorps.Id);
        }

        [Test]
        public void ScoreRewardsSpeedAndFewCasualties()
        {
            var map = Open(4, 1);
            var p = U(1, Side.Player, 1, 0, 0, Stats(str: 30));
            var e = U(2, Side.Enemy, 2, 1, 0);
            var b = Make(map, new[] { p, e }, new[] { C(1, Side.Player, 1, p) }, new[] { C(2, Side.Enemy, 2, e) }, turnLimit: 10);
            b.Start();
            Act(b, UnitCommand.Attack(p, p.Pos, e));

            var s = b.Score(Side.Player);
            Assert.AreEqual(100, s.Speed);
            Assert.AreEqual(100, s.Casualty);
            Assert.AreEqual(100, s.Total);
        }
    }
}
