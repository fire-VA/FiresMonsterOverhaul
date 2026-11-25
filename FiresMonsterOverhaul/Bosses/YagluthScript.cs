using BepInEx.Configuration;
using FiresMonsterOverhaul.Abilities;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// 0.0.40 (Fire): Yagluth ("GoblinKing"). Two thirds: a fire ring bursts round him (Blast.Broadcast, the ring shows
    /// first, a share of each hit player's max health like Thunderhead). One third: Fulings (Goblin) come to his aid.
    /// His vanilla meteors, flame breath and nova stay as they are.
    /// </summary>
    internal sealed class YagluthScript : TwoPhaseBossScript
    {
        private const float RingDelay = 2f;

        protected override ConfigEntry<bool> Enabled => MonsterConfig.YagluthPhases;
        internal override ConfigEntry<float> FirstAt => MonsterConfig.YagluthFirstPhase;
        internal override ConfigEntry<float> SecondAt => MonsterConfig.YagluthSecondPhase;

        protected override void OnFirstPhase()
        {
            float radius = MonsterConfig.YagluthFireRingRadius.Value;
            float share = MonsterConfig.YagluthFireRingDamage.Value;
            var at = D.transform.position;
            Blast.Broadcast(at, radius, RingDelay, -share, "ability: fire ring");
            MonsterLog.Debug($"boss: {D.Prefab} fire ring at {at:F1} (r {radius:0.#} m, {RingDelay:0.0} s warning, {share:P0} of each player's max health) {D.Zdo.m_uid}");
        }

        protected override void OnSecondPhase() =>
            SpawnAdds(MonsterConfig.YagluthFulingPrefab.Value, MonsterConfig.YagluthFulings.Value, MonsterConfig.YagluthFulingsMax.Value, "Fuling(s) at one third");
    }
}
