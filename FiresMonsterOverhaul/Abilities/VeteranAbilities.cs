using FiresMonsterOverhaul.Casts;
using FiresMonsterOverhaul.Roles;
using FiresMonsterOverhaul.Tiers;
using UnityEngine;

namespace FiresMonsterOverhaul.Abilities
{
    /// <summary>
    /// Doc 21: a Veteran gets "a second ability" (and Elites and Champions with it: tiers stack). One per role, each
    /// telegraphed and answered by that role's counter (32-PHASE3-PLAN.md "Slice 2b"):
    ///   Caster     - Ground Blast: an interruptible cast, a red ring where its target stood, then a blast in the ring.
    ///   Brute      - Charge: an uninterruptible wind-up, a red ring at the landing spot, a rush there, a slam in the ring.
    ///   Skirmisher - Sidestep: right after it attacks, it darts aside (hit and run).
    /// Runs on the owner on a 0.5 s tick; the charge and the sidestep move it through vanilla MoveTo from the
    /// MonsterAI.UpdateAI patch (OverrideAI), so vanilla's AI simply pauses while one runs.
    /// </summary>
    public sealed class VeteranAbilities : MonoBehaviour
    {
        private const float Tick = 0.5f;
        private const float BlastMinRange = 4f;
        private const float BlastMaxRange = 30f;
        private const float ChargeMinRange = 6f;
        private const float ChargeMaxRange = 16f;
        private const float ChargeMaxSeconds = 2.5f;
        private const float ChargeArriveDistance = 1.5f;
        private const float SidestepDelay = 0.9f;
        private const float SidestepSeconds = 1.4f;
        private const float SidestepBack = 1.5f;
        private const float MoveStopDistance = 0.5f;
        private const string ChargeEffectName = "FMO_Charge";

        private static SE_Stats _chargeEffect;

        private ZNetView _nview;
        private Character _character;
        private MonsterAI _ai;
        private MonsterCast _cast;
        private float _nextTick;
        private float _nextAbility;

        private bool _charging;
        private Vector3 _chargeSpot;
        private float _chargeUntil;
        private float _sidestepAt;
        private Vector3 _moveTarget;
        private float _moveUntil;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _character = GetComponent<Character>();
            _ai = GetComponent<MonsterAI>();
            _nextTick = Time.time + Random.Range(0f, Tick);
            Blast.EnsureRegistered();
        }

        /// <summary>The fmo_cast_ready test hook: the ability may go again at once.</summary>
        public void ResetCooldowns() => _nextAbility = 0f;

        private MonsterRole Role()
        {
            var tag = GetComponent<MonsterRoleTag>();
            return tag != null ? tag.Role : MonsterRole.None;
        }

        private bool Active(out Character target)
        {
            target = null;
            if (!MonsterConfig.Enabled.Value || !MonsterConfig.VeteranAbilities.Value) return false;
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _character == null || _character.IsDead()) return false;
            if (!MonsterTierTag.Eligible(_character, _nview) || MonsterTierTag.TierOf(_nview.GetZDO()) < MonsterTier.Veteran) return false;
            target = _ai != null ? _ai.m_targetCreature : null;
            return target != null && !target.IsDead();
        }

        private void Update()
        {
            long perfT = FiresMonsterOverhaul.Perf.FmoPerf.Start();
            try { UpdateBody(); }
            finally { FiresMonsterOverhaul.Perf.FmoPerf.Stop(FiresMonsterOverhaul.Perf.FmoPerf.Path.VeteranAbilities, perfT); }
        }

        private void UpdateBody()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + Tick;
            if (!Active(out var target)) { _charging = false; _sidestepAt = 0f; return; }

            switch (Role())
            {
                case MonsterRole.Caster: TryGroundBlast(target); break;
                case MonsterRole.Brute: TryCharge(target); break;
                case MonsterRole.Skirmisher:
                    if (_sidestepAt > 0f && Time.time >= _sidestepAt && !_character.InAttack()) Sidestep(target); // its attack animation holds it still (round 15: 1.4 of 4 m)
                    break;
            }
        }

        private MonsterCast Cast()
        {
            if (_cast == null) _cast = GetComponent<MonsterCast>() ?? Patches.HoldWhileCastingPatch.Added(gameObject.AddComponent<MonsterCast>());
            return _cast;
        }

        private float Distance(Character target) => Vector3.Distance(transform.position, target.transform.position);

        // Caster: an interruptible cast; the ring marks where the target stood when it began.
        private void TryGroundBlast(Character target)
        {
            if (Time.time < _nextAbility) return;
            float d = Distance(target);
            if (d < BlastMinRange || d > BlastMaxRange || !_ai.CanSeeTarget(target)) return;
            var cast = Cast();
            if (cast.Active) return;
            Vector3 spot = target.transform.position;
            float radius = MonsterConfig.GroundBlastRadius.Value;
            float seconds = MonsterConfig.GroundBlastCastSeconds.Value;
            float damage = _character.GetMaxHealth() * MonsterConfig.GroundBlastDamage.Value;
            string prefab = Utils.GetPrefabName(gameObject);
            bool begun = cast.Begin("ground blast", seconds, true, 3, radius, () =>
            {
                Blast.Broadcast(spot, radius, 0f, damage, "ability: ground blast");
            }, holdStill: true, ringCenter: spot);
            if (!begun) return;
            _nextAbility = Time.time + MonsterConfig.GroundBlastCooldown.Value;
            MonsterLog.Debug($"ability: {prefab} ground blast at {spot:F1} on {target.GetHoverName()} (r {radius:0.#} m, {seconds:0.0} s, {damage:0.#} damage) {_nview.GetZDO().m_uid}");
        }

        // Brute: an uninterruptible wind-up with the landing ring shown until the slam, then a rush and a slam.
        private void TryCharge(Character target)
        {
            if (_charging || Time.time < _nextAbility) return;
            float d = Distance(target);
            if (d < ChargeMinRange || d > ChargeMaxRange || !_ai.CanSeeTarget(target)) return;
            var cast = Cast();
            if (cast.Active) return;
            Vector3 spot = target.transform.position;
            float radius = MonsterConfig.ChargeSlamRadius.Value;
            float windup = MonsterConfig.ChargeWindupSeconds.Value;
            bool begun = cast.Begin("charge", windup, false, 3, radius, () => StartCharge(spot),
                holdStill: true, ringCenter: spot, ringSeconds: windup + ChargeMaxSeconds);
            if (!begun) return;
            _nextAbility = Time.time + MonsterConfig.ChargeCooldown.Value;
            MonsterLog.Debug($"ability: {Utils.GetPrefabName(gameObject)} charge wind-up at {spot:F1} on {target.GetHoverName()} ({d:0.0} m away, {windup:0.0} s) {_nview.GetZDO().m_uid}");
        }

        private void StartCharge(Vector3 spot)
        {
            _charging = true;
            _chargeSpot = spot;
            _chargeUntil = Time.time + ChargeMaxSeconds;
            if (_chargeEffect == null)
            {
                _chargeEffect = ScriptableObject.CreateInstance<SE_Stats>();
                _chargeEffect.name = ChargeEffectName;
                _chargeEffect.m_name = ChargeEffectName;
            }
            _chargeEffect.m_ttl = ChargeMaxSeconds;
            _chargeEffect.m_speedModifier = MonsterConfig.ChargeSpeedBonus.Value;
            _character.GetSEMan().AddStatusEffect(_chargeEffect, true); // SEMan adds a clone (same name hash)
            MonsterLog.Debug($"ability: {Utils.GetPrefabName(gameObject)} charging to {spot:F1} (+{MonsterConfig.ChargeSpeedBonus.Value:P0} speed, {ChargeMaxSeconds:0.0} s max)");
        }

        private void EndCharge(bool slam)
        {
            _charging = false;
            _character.GetSEMan().RemoveStatusEffect(ChargeEffectName.GetStableHashCode(), true);
            string prefab = Utils.GetPrefabName(gameObject);
            float off = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(_chargeSpot.x, 0f, _chargeSpot.z));
            if (!slam)
            {
                MonsterLog.Debug($"ability: {prefab} charge ended short ({off:0.0} m from the ring): no slam");
                return;
            }
            float radius = MonsterConfig.ChargeSlamRadius.Value;
            float damage = _character.GetMaxHealth() * MonsterConfig.ChargeSlamDamage.Value;
            MonsterLog.Debug($"ability: {prefab} charge slam at {_chargeSpot:F1} ({damage:0.#} damage, r {radius:0.#} m; arrived {off:0.0} m from the centre) {_nview.GetZDO().m_uid}");
            Blast.Broadcast(_chargeSpot, radius, 0f, damage, "ability: charge slam");
        }

        /// <summary>Called by the Humanoid.StartAttack postfix: a Skirmisher darts aside right after it attacks.</summary>
        internal void OnAttackStarted()
        {
            if (Role() != MonsterRole.Skirmisher || Time.time < _nextAbility || _sidestepAt > 0f) return;
            if (!Active(out _)) return;
            _sidestepAt = Time.time + SidestepDelay;
        }

        private void Sidestep(Character target)
        {
            _sidestepAt = 0f;
            _nextAbility = Time.time + MonsterConfig.SidestepCooldown.Value;
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget = RingUp.Flat(toTarget, transform.position);   // 0.0.46: halo ring plane
            if (toTarget.sqrMagnitude < 0.01f) toTarget = transform.forward;
            toTarget.Normalize();
            bool left = Random.value < 0.5f;
            Vector3 side = Vector3.Cross(RingUp.Up(transform.position), toTarget) * (left ? -1f : 1f);
            float distance = MonsterConfig.SidestepDistance.Value;
            _moveTarget = transform.position + side * distance - toTarget * SidestepBack;
            _moveUntil = Time.time + SidestepSeconds;
            MonsterLog.Debug($"ability: {Utils.GetPrefabName(gameObject)} sidestep {distance:0.#} m {(left ? "left" : "right")} after its attack {_nview.GetZDO().m_uid}");
        }

        /// <summary>
        /// From the MonsterAI.UpdateAI prefix: true while a charge or a sidestep drives the creature (vanilla's AI
        /// tick is skipped for that frame).
        /// </summary>
        /// <summary>
        /// A straight-line dash (a charge, a sidestep, a boss's charge lane). Not vanilla MoveTo: its pathfinding reuses a
        /// result for up to 1 s, so a fresh target got a stale "no path" and the creature stood still (round 13, 10-08:
        /// a Troll's charge ended 11.0 m out, where it started; a sidestep moved 0.6 of 4 m).
        /// </summary>
        internal static void Dash(MonsterAI ai, Vector3 target)
        {
            Vector3 d = target - ai.transform.position;
            d = RingUp.Flat(d, ai.transform.position);   // 0.0.46: halo ring plane
            if (d.magnitude <= MoveStopDistance) { ai.StopMoving(); return; }
            ai.MoveTowards(d, true);
        }

        internal bool OverrideAI(MonsterAI ai, float dt)
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return false;
            if (_charging)
            {
                Vector3 flat = transform.position - _chargeSpot;
                flat = RingUp.Flat(flat, transform.position);   // 0.0.46: halo ring plane
                if (flat.magnitude <= ChargeArriveDistance) { EndCharge(true); return false; }
                if (Time.time > _chargeUntil || _character.IsDead()) { EndCharge(false); return false; }
                Dash(ai, _chargeSpot);
                return true;
            }
            if (Time.time < _moveUntil)
            {
                Dash(ai, _moveTarget);
                return true;
            }
            return false;
        }
    }
}
