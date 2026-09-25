using System.Collections.Generic;

namespace Srpg.Core.Combat
{
    public enum CommandLevel
    {
        Manual = 1,
        Fine = 2,
        Medium = 3,
        Coarse = 4,
        Auto = 5,
    }

    public enum OrderType
    {
        Attack,
        Hold,
        Retreat,
        Follow,
    }

    public sealed class CorpsOrder
    {
        public OrderType Type = OrderType.Attack;
        public bool HasTarget;
        public GridPos Target;
        public int FollowCorpsId = -1;
        public int HoldRadius = 3;

        public static CorpsOrder AttackNearest() => new CorpsOrder();

        public static CorpsOrder AttackAt(GridPos p) =>
            new CorpsOrder { Type = OrderType.Attack, HasTarget = true, Target = p };

        public static CorpsOrder HoldAt(GridPos p, int radius) =>
            new CorpsOrder { Type = OrderType.Hold, HasTarget = true, Target = p, HoldRadius = radius };

        public static CorpsOrder RetreatTo(GridPos p) =>
            new CorpsOrder { Type = OrderType.Retreat, HasTarget = true, Target = p };

        public static CorpsOrder Follow(int corpsId) =>
            new CorpsOrder { Type = OrderType.Follow, FollowCorpsId = corpsId };
    }

    public sealed class Corps
    {
        public int Id;
        public string Name;
        public Side Side;
        public int LeaderId = -1;
        public readonly List<int> Members = new List<int>();
        public CommandLevel Level = CommandLevel.Auto;
        public CorpsOrder Order = CorpsOrder.AttackNearest();

        public override string ToString() => Name;
    }
}
