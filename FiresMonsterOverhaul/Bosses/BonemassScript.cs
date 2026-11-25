namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// Bonemass (doc 22 §3; 34-PHASE4 slice 3): Feeding at two thirds and one third (Blobs crawl to him and each heals him
    /// on arrival: kill or slow them on the way), and Plague (PlagueEffect: his hits stack it on a player; at Plague Stacks
    /// it bursts on everyone nearby unless it's cleansed first). His vanilla poison cloud, skeletons and blobs stay.
    /// </summary>
    internal sealed class BonemassScript : BossScript
    {
        private const int PhaseTwoThirds = 1 << 0;
        private const int PhaseOneThird = 1 << 1;

        public override void Tick(bool inCombat, float healthFraction)
        {
            string feeder = MonsterConfig.BonemassFeederPrefab.Value;
            if (healthFraction <= 2f / 3f && D.FirePhase(PhaseTwoThirds, "two thirds", healthFraction))
                D.StartPattern(HealPattern.Feeding, "phase two thirds", feeder);
            if (healthFraction <= 1f / 3f && D.FirePhase(PhaseOneThird, "one third", healthFraction))
                D.StartPattern(HealPattern.Feeding, "phase one third", feeder);
        }
    }
}
