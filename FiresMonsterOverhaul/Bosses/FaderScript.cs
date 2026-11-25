using BepInEx.Configuration;
using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// 0.0.40 (Fire): Fader. Two thirds: a field of burning rings spreads round him (the Elemental affix's fire ground,
    /// AffixRunner.BroadcastHazard, Fader Hazard Rings of them between 4 and 12 m out). One third: Charred (Charred_Melee)
    /// come to his aid. His vanilla attacks stay as they are.
    /// </summary>
    internal sealed class FaderScript : TwoPhaseBossScript
    {
        private const float FieldInner = 4f;
        private const float FieldOuter = 12f;
        private const int FireElement = 0;

        protected override ConfigEntry<bool> Enabled => MonsterConfig.FaderPhases;
        internal override ConfigEntry<float> FirstAt => MonsterConfig.FaderFirstPhase;
        internal override ConfigEntry<float> SecondAt => MonsterConfig.FaderSecondPhase;

        protected override void OnFirstPhase()
        {
            int count = Mathf.Max(0, MonsterConfig.FaderHazardRings.Value);
            float radius = MonsterConfig.HazardRadius.Value;
            float seconds = MonsterConfig.HazardSeconds.Value;
            float dps = MonsterConfig.FaderHazardDamage.Value;
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float angle = (start + i * 360f / count) * Mathf.Deg2Rad;
                Vector3 at = D.transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * Random.Range(FieldInner, FieldOuter);
                RingUp.Ground(ref at, 0f, "fmo spawn");   // 0.0.46: the ring floor on a halo ring, else the heightmap as before
                Affixes.AffixRunner.BroadcastHazard(at, radius, seconds, dps, FireElement);
            }
            MonsterLog.Debug($"boss: {D.Prefab} burning field: {count} fire ring(s) {FieldInner:0}-{FieldOuter:0} m out (r {radius:0.#} m, {seconds:0} s, {dps:0.#}/s) {D.Zdo.m_uid}");
        }

        protected override void OnSecondPhase() =>
            SpawnAdds(MonsterConfig.FaderCharredPrefab.Value, MonsterConfig.FaderCharred.Value, MonsterConfig.FaderCharredMax.Value, "Charred at one third");
    }
}
