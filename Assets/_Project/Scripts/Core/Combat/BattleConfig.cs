using System;

namespace Srpg.Core.Combat
{
    // All provisional battle numbers live here (GDD 13).
    [Serializable]
    public sealed class BattleConfig
    {
        public int HitPerSkill = 2;
        public int AvoidPerSpeed = 2;
        public int DoubleAttackSpeedGap = 4;

        public int MoraleMax = 100;
        public int WaverThreshold = 30;
        public int WaverHitPenalty = 10;
        public int WaverAvoidPenalty = 10;

        // Losing all max HP in one hit costs this much morale.
        public int MoraleLossFullHpDamage = 50;
        public int MoraleLossAllyDown = 12;
        public int AllyDownRadius = 3;
        public int MoraleLossLeaderDown = 35;
        public int MoraleGainOnDefeat = 5;
        public int MoraleRecoverBase = 5;
        public int MoraleRecoverInAura = 10;

        public int AuraBaseRadius = 2;
        public int AuraLeadershipPerRadius = 10;
        public int AuraHitDivisor = 2;
        public int AuraAvoidDivisor = 4;
        public int AuraMoraleLossReductionPerLeadership = 2;
        public int AuraMoraleLossReductionMax = 60;

        public int CarryMovePenaltyMax = 2;
        public int CarryStatPenaltyMaxPercent = 50;

        public int SpeedScoreWeight = 50;
        public int CasualtyScoreWeight = 50;

        public TerrainInfo Terrain(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.Plain: return new TerrainInfo(1, 0, 0);
                case TerrainType.Forest: return new TerrainInfo(2, 20, 1);
                case TerrainType.Mountain: return new TerrainInfo(3, 30, 2);
                case TerrainType.Fort: return new TerrainInfo(1, 20, 3);
                default: return new TerrainInfo(TerrainInfo.Impassable, 0, 0);
            }
        }
    }
}
