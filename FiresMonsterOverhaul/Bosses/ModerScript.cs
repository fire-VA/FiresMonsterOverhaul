using BepInEx.Configuration;
using FiresMonsterOverhaul.Abilities;
using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// 0.0.40 (Fire): Moder ("Dragon"). Two thirds: drakes (Hatchling) come to her aid. One third: frost lanes, the charge
    /// lane's telegraph (Blast.BroadcastLane) from her to a player, then the Elemental affix's frost rings along it
    /// (AffixRunner.BroadcastHazard); one as the phase starts, then every Moder Frost Lane Cooldown in combat.
    /// Her vanilla breath, spit and landing stay as they are.
    /// </summary>
    internal sealed class ModerScript : TwoPhaseBossScript
    {
        private const float LaneRange = 40f;
        private const float LaneMaxLength = 30f;
        private const float LaneWindup = 1.5f;
        private const int FrostElement = 1;

        private float _nextLane;
        private float _laneLandsAt = -1f;
        private Vector3 _laneFrom, _laneTo;

        protected override ConfigEntry<bool> Enabled => MonsterConfig.ModerPhases;
        internal override ConfigEntry<float> FirstAt => MonsterConfig.ModerFirstPhase;
        internal override ConfigEntry<float> SecondAt => MonsterConfig.ModerSecondPhase;

        protected override void OnFirstPhase() =>
            SpawnAdds(MonsterConfig.ModerDrakePrefab.Value, MonsterConfig.ModerDrakes.Value, MonsterConfig.ModerDrakesMax.Value, "drake(s) at two thirds");

        protected override void OnSecondPhase()
        {
            _nextLane = 0f;
            TryFrostLane(true);
        }

        protected override void TickPhases(bool inCombat)
        {
            if (_laneLandsAt > 0f && Time.time >= _laneLandsAt) LandFrostLane();
            if (inCombat && InSecond) TryFrostLane(false);
        }

        private void TryFrostLane(bool phaseEntry)
        {
            if (Time.time < _nextLane || _laneLandsAt > 0f) return;
            var target = D.Target ?? NearestPlayer();
            if (target == null)
            {
                if (phaseEntry) MonsterLog.Debug($"boss: {D.Prefab} frost lane: no player within {LaneRange:0} m {D.Zdo.m_uid}");
                return;
            }
            Vector3 to = target.transform.position;
            Vector3 from = new Vector3(D.transform.position.x, to.y, D.transform.position.z); // she may be flying: the lane is on the ground
            Vector3 dir = to - from;
            if (dir.magnitude > LaneMaxLength) to = from + dir.normalized * LaneMaxLength;
            float halfWidth = MonsterConfig.HazardRadius.Value;
            _laneFrom = from;
            _laneTo = to;
            _laneLandsAt = Time.time + LaneWindup;
            _nextLane = Time.time + MonsterConfig.ModerFrostLaneCooldown.Value;
            Blast.BroadcastLane(from, to, halfWidth, LaneWindup + MonsterConfig.HazardSeconds.Value);
            MonsterLog.Debug($"boss: {D.Prefab} frost lane to {to:F1} ({Vector3.Distance(from, to):0.0} m, {LaneWindup:0.0} s wind-up) on {target.GetHoverName()} {D.Zdo.m_uid}");
        }

        // The lane freezes: frost rings every ring-width along it, each the Elemental affix's frost ground.
        private void LandFrostLane()
        {
            _laneLandsAt = -1f;
            float radius = MonsterConfig.HazardRadius.Value;
            float seconds = MonsterConfig.HazardSeconds.Value;
            float dps = MonsterConfig.ModerFrostLaneDamage.Value;
            float length = Vector3.Distance(_laneFrom, _laneTo);
            int rings = Mathf.Max(1, Mathf.CeilToInt(length / (radius * 2f)));
            for (int i = 0; i <= rings; i++)
            {
                Vector3 at = Vector3.Lerp(_laneFrom, _laneTo, i / (float)rings);
                RingUp.Ground(ref at, 0f, "fmo spawn");   // 0.0.46: the ring floor on a halo ring, else the heightmap as before
                Affixes.AffixRunner.BroadcastHazard(at, radius, seconds, dps, FrostElement);
            }
            MonsterLog.Debug($"boss: {D.Prefab} frost lane froze: {rings + 1} frost ring(s) (r {radius:0.#} m, {seconds:0} s, {dps:0.#}/s) {D.Zdo.m_uid}");
        }

        private Player NearestPlayer()
        {
            Player best = null;
            float bestD = LaneRange;
            foreach (var p in Player.GetAllPlayers())
            {
                if (p == null || p.IsDead()) continue;
                float d = Vector3.Distance(p.transform.position, D.transform.position);
                if (d <= bestD) { best = p; bestD = d; }
            }
            return best;
        }

        public override void ResetCooldowns() => _nextLane = 0f;
    }
}
