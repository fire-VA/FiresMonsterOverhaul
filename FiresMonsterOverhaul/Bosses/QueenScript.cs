using BepInEx.Configuration;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// 0.0.40 (Fire): The Queen ("SeekerQueen"). Two thirds: Seekers come to her aid. One third: she Entangles players, the
    /// Elder's Entangle (EntangleCaster: same ring, roots, keys and log lines), the first as the phase starts if a player
    /// is in range, then every Elder Entangle Cooldown in combat. Her vanilla attacks and brood spawns stay as they are.
    /// </summary>
    internal sealed class QueenScript : TwoPhaseBossScript
    {
        private EntangleCaster _entangle;

        protected override ConfigEntry<bool> Enabled => MonsterConfig.QueenPhases;
        internal override ConfigEntry<float> FirstAt => MonsterConfig.QueenFirstPhase;
        internal override ConfigEntry<float> SecondAt => MonsterConfig.QueenSecondPhase;

        private EntangleCaster Caster => _entangle ?? (_entangle = new EntangleCaster(D));

        protected override void OnFirstPhase() =>
            SpawnAdds(MonsterConfig.QueenSeekerPrefab.Value, MonsterConfig.QueenSeekers.Value, MonsterConfig.QueenSeekersMax.Value, "Seeker(s) at two thirds");

        protected override void OnSecondPhase()
        {
            Caster.ResetCooldowns();
            Caster.TryEntangle();
        }

        protected override void TickPhases(bool inCombat)
        {
            Caster.WitherRoots();
            if (inCombat && InSecond) Caster.TryEntangle();
        }

        public override void OnDeath() => _entangle?.OnDeath();

        public override void ResetCooldowns() => _entangle?.ResetCooldowns();
    }
}
