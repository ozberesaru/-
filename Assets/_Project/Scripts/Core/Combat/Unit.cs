namespace Srpg.Core.Combat
{
    public enum Side
    {
        Player,
        Enemy,
    }

    public enum UnitStatus
    {
        Active,
        Routed,
        Downed,
        Stunned,
        Surrendered,
        Carried,
        Escaped,
    }

    public struct UnitStats
    {
        public int MaxHp;
        public int Str;
        public int Skl;
        public int Spd;
        public int Def;
        public int Bld;
        public int Ldr;
        public int Mov;
    }

    public sealed class Weapon
    {
        public string Name;
        public int Might;
        public int Hit;
        public int MinRange = 1;
        public int MaxRange = 1;
        public bool NonLethal;
    }

    public sealed class Unit
    {
        public int Id;
        public string Name;
        public Side Side;
        public int CorpsId;
        public UnitStats Stats;
        public Weapon Weapon;

        public GridPos Pos;
        public int Hp;
        public int Morale;
        public UnitStatus Status;
        public int CarryingId = -1;
        public int CarriedById = -1;
        public UnitStatus StatusBeforeCarry;
        public bool HasActed;

        public bool OnMap => Status == UnitStatus.Active || Status == UnitStatus.Routed ||
                             Status == UnitStatus.Stunned || Status == UnitStatus.Surrendered;

        // Routed units are still on the field and can rally, so they keep the battle going.
        public bool InFight => Status == UnitStatus.Active || Status == UnitStatus.Routed;

        public bool Capturable => Status == UnitStatus.Stunned || Status == UnitStatus.Surrendered;

        public bool IsCarrying => CarryingId >= 0;

        public override string ToString() => $"{Name}#{Id}";
    }
}
