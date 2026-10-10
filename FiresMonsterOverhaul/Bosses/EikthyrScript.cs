using System.Collections.Generic;
using FiresMonsterOverhaul.Abilities;
using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// Eikthyr, "the teaching fight" (doc 22): Thunderhead, a long interruptible cast that teaches stagger-to-interrupt,
    /// and a charge lane. No healing phase. At 50 %: spirit stags, small crystal-Aspect copies of him (Fire's pick, SpiritForm).
    /// </summary>
    internal sealed class EikthyrScript : BossScript
    {
        private const int PhaseHalf = 1 << 0;
        private const float HalfHealth = 0.5f;
        private const float LaneMinRange = 8f;
        private const float LaneMaxRange = 25f;
        private const float LaneChargeSeconds = 2.5f;
        private const float LaneSpeedBonus = 1f;
        private const float ArriveDistance = 1.5f;
        private const float MoveStopDistance = 0.5f;
        private const string ChargeEffectName = "FMO_BossCharge";
        private const float SpiritOffset = 4f;
        private const string SpiritStagName = "Spirit Stag";

        private static SE_Stats _chargeEffect;
        private float _nextThunder;
        private float _nextLane;
        private bool _charging;
        private Vector3 _laneFrom;
        private Vector3 _laneTo;
        private float _chargeUntil;

        public override void Tick(bool inCombat, float healthFraction)
        {
            if (healthFraction <= HalfHealth && D.FirePhase(PhaseHalf, "half", healthFraction)) SpawnSpiritStags();
            if (!inCombat || _charging) return;
            TryThunderhead();
            TryChargeLane();
        }

        // Fire 10-08 (via the lead): "the small eik copies using the deep north boss summon material" (SpiritForm).
        private void SpawnSpiritStags()
        {
            int count = Adds.CountFor(D.transform.position, MonsterConfig.EikthyrSpiritStags.Value, MonsterConfig.EikthyrSpiritStagsMax.Value);
            float share = MonsterConfig.SpiritHealth.Value;
            float scale = MonsterConfig.SpiritScale.Value;
            var ids = new List<ZDOID>();
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float angle = (start + i * 360f / count) * Mathf.Deg2Rad;
                Vector3 at = D.transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * SpiritOffset;
                var stag = Adds.SpawnCopy(D.Body, D.Prefab, at, 1, D.transform.rotation, (c, z) =>
                {
                    z.Set(Affixes.AffixRunner.ZdoSummonedBy, D.Zdo.m_uid);
                    SpiritForm.Make(c, z, SpiritStagName, share, scale);
                });
                if (stag != null) ids.Add(stag.GetZDOID());
            }
            MonsterLog.Debug($"boss: {D.Prefab} adds: {ids.Count} spirit stag(s) [{string.Join(", ", ids)}] ({share:P0} of his health, scale {scale:0.##}) {D.Zdo.m_uid}");
        }

        // A long, interruptible cast; the ring round him shows where the lightning lands.
        private void TryThunderhead()
        {
            if (Time.time < _nextThunder) return;
            var cast = D.Cast();
            if (cast.Active) return;
            float seconds = MonsterConfig.ThunderheadCastSeconds.Value;
            float radius = MonsterConfig.ThunderheadRadius.Value;
            float share = MonsterConfig.ThunderheadDamage.Value;
            var body = D.transform;
            if (!cast.Begin("thunderhead", seconds, true, 3, radius, () =>
            {
                Blast.Broadcast(body.position, radius, 0f, -share, "ability: thunderhead");
            }, holdStill: true)) return;
            _nextThunder = Time.time + MonsterConfig.ThunderheadCooldown.Value;
            MonsterLog.Debug($"boss: {D.Prefab} thunderhead cast ({seconds:0.0} s, interruptible, r {radius:0.#} m, {share:P0} of each player's max health) {D.Zdo.m_uid}");
        }

        // The lane shows from him to his target, then he charges down it; whoever is still in it is hit.
        private void TryChargeLane()
        {
            if (Time.time < _nextLane) return;
            var target = D.Target;
            if (target == null || D.Ai == null) return;
            float d = Vector3.Distance(D.transform.position, target.transform.position);
            if (d < LaneMinRange || d > LaneMaxRange || !D.Ai.CanSeeTarget(target)) return;
            var cast = D.Cast();
            if (cast.Active) return;
            float windup = MonsterConfig.ChargeLaneWindupSeconds.Value;
            Vector3 from = D.transform.position;
            Vector3 to = target.transform.position;
            if (!cast.Begin("charge lane", windup, false, 0, 0f, () => StartCharge(), holdStill: true)) return;
            _laneFrom = from;
            _laneTo = to;
            _nextLane = Time.time + MonsterConfig.ChargeLaneCooldown.Value;
            Blast.BroadcastLane(from, to, MonsterConfig.ChargeLaneWidth.Value * 0.5f, windup + LaneChargeSeconds);
            MonsterLog.Debug($"boss: {D.Prefab} charge lane to {to:F1} ({d:0.0} m, {windup:0.0} s wind-up) on {target.GetHoverName()} {D.Zdo.m_uid}");
        }

        private void StartCharge()
        {
            _charging = true;
            _chargeUntil = Time.time + LaneChargeSeconds;
            if (_chargeEffect == null)
            {
                _chargeEffect = ScriptableObject.CreateInstance<SE_Stats>();
                _chargeEffect.name = ChargeEffectName;
                _chargeEffect.m_name = ChargeEffectName;
            }
            _chargeEffect.m_ttl = LaneChargeSeconds;
            _chargeEffect.m_speedModifier = LaneSpeedBonus;
            D.Body.GetSEMan().AddStatusEffect(_chargeEffect, true);
            // Whoever is still in the lane when the wind-up ends is hit as he sets off (round 15: judged at the end of the
            // charge, his body had shoved a player who stood still 1.7 m out of the lane).
            Blast.BroadcastLaneHit(_laneFrom, _laneTo, MonsterConfig.ChargeLaneWidth.Value * 0.5f, -MonsterConfig.ChargeLaneDamage.Value, "ability: charge lane");
        }

        private void EndCharge()
        {
            _charging = false;
            D.Body.GetSEMan().RemoveStatusEffect(ChargeEffectName.GetStableHashCode(), true);
            Vector3 p = D.transform.position;
            float off = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(_laneTo.x, _laneTo.z));
            MonsterLog.Debug($"boss: {D.Prefab} charge lane ended {off:0.0} m from its end {D.Zdo.m_uid}");
        }

        public override bool OverrideAI(MonsterAI ai, float dt)
        {
            if (!_charging) return false;
            Vector3 p = D.transform.position;
            bool arrived = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(_laneTo.x, _laneTo.z)) <= ArriveDistance;
            if (arrived || Time.time > _chargeUntil || D.Body.IsDead()) { EndCharge(); return false; }
            Abilities.VeteranAbilities.Dash(ai, _laneTo);
            return true;
        }

        public override void ResetCooldowns()
        {
            _nextThunder = 0f;
            _nextLane = 0f;
        }
    }
}
