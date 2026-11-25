using System.Collections.Generic;
using FiresCore.Npc.Archetypes;
using FiresCore.Npc.Combat.Agent;
using UnityEngine;

namespace FiresMonsterOverhaul.Agent
{
    /// <summary>
    /// ICombatAgent over a vanilla MonsterAI + Humanoid body (Core Docs/PLAN_CombatAgent.md). Movement and facing go
    /// through MonsterAI's own MoveTo / LookAt, so vanilla stays the single writer of the body's move direction.
    /// A creature keeps no stamina or weapon skills, so Stamina and Skills are null and behaviours spend nothing.
    /// CombatAgents.Find picks this component up through GetComponent&lt;ICombatAgent&gt;; no Core change needed.
    /// </summary>
    public sealed class MonsterAgent : MonoBehaviour, ICombatAgent
    {
        private static readonly List<Character> ScanBuffer = new List<Character>();

        private Character _character;
        private Humanoid _humanoid;
        private MonsterAI _ai;
        private ZNetView _nview;
        private CombatAgentCapability _capabilities;

        private void Awake()
        {
            _character = GetComponent<Character>();
            _humanoid = _character as Humanoid;
            _ai = GetComponent<MonsterAI>();
            _nview = GetComponent<ZNetView>();
            var animator = GetComponentInChildren<Animator>();
            _capabilities = AnimatorCapabilityProbe.Probe(_character, animator) | CombatAgentCapability.Sidestep | CombatAgentCapability.Cast;
        }

        public Character Body => _character;
        public Humanoid Humanoid => _humanoid;
        public BaseAI Ai => _ai;
        public MonsterAI MonsterAi => _ai;
        public ZNetView NView => _nview;
        public Transform Transform => transform;

        public Vector3 Position => transform.position;
        public Vector3 EyePosition => _character != null ? _character.GetEyePoint() : transform.position;
        public float Radius => _character != null ? _character.GetRadius() : 0.5f;

        public Character.Faction Faction => _character != null ? _character.GetFaction() : Character.Faction.Players;
        public string FactionGroup => _character != null ? _character.GetGroup() : string.Empty;

        public bool IsOwner => _nview != null && _nview.IsValid() && _nview.IsOwner();
        public bool IsAlive => _character != null && !_character.IsDead();
        public float HealthFraction => _character != null ? _character.GetHealthPercentage() : 0f;
        public bool IsStaggering => _character != null && _character.IsStaggering();

        public CombatAgentCapability Capabilities => _capabilities;
        public bool Can(CombatAgentCapability capability) => (_capabilities & capability) == capability;

        public Character Target => _ai != null ? _ai.m_targetCreature : null;

        public bool TrySetTarget(Character target)
        {
            if (_ai == null || !IsOwner) return false;
            if (target != null && !_ai.IsEnemy(target)) return false;
            _ai.m_targetCreature = target;
            return true;
        }

        public bool MoveToward(Vector3 point, bool run, float reachDistance)
        {
            if (_ai == null || !IsOwner) return false;
            return _ai.MoveTo(Time.deltaTime, point, reachDistance, run);
        }

        public void LookToward(Vector3 point)
        {
            if (_ai != null && IsOwner) _ai.LookAt(point);
        }

        public void StopMoving()
        {
            if (_ai != null && IsOwner) _ai.StopMoving();
        }

        public bool IsAttacking => _humanoid != null && _humanoid.InAttack();

        public bool TryAttack(Character target)
        {
            if (_humanoid == null || !IsOwner || target == null) return false;
            return _humanoid.StartAttack(target, false);
        }

        public bool ForceAttackNow() => TryAttack(Target);

        public ICombatStamina Stamina => null;
        public ICombatSkills Skills => null;

        public bool HasFacingAuthority => false;
        public bool TryFaceThrough(string owner, Vector3 point, CombatFacingPriority priority, float holdSeconds)
        {
            LookToward(point);
            return true;
        }

        public bool HasClearShotTo(Character target) => _ai != null && target != null && _ai.CanSeeTarget(target);
        public bool HasClearShotToCurrentTarget() => HasClearShotTo(Target);
        public void BlacklistTargetForLineOfSight(Character target) { }

        public bool IsBlocking => _humanoid != null && _humanoid.IsBlocking();

        public bool TryBlock()
        {
            if (_humanoid == null || !IsOwner || !Can(CombatAgentCapability.Block)) return false;
            _humanoid.m_blocking = true;
            return true;
        }

        public void ReleaseBlock()
        {
            if (_humanoid != null && IsOwner) _humanoid.m_blocking = false;
        }

        public bool TryDodgeAwayFrom(Character threat)
        {
            if (_ai == null || !IsOwner || threat == null) return false;
            Vector3 away = transform.position - threat.transform.position;
            away = RingUp.Flat(away, transform.position);   // 0.0.46: halo ring plane
            if (away.sqrMagnitude < 0.01f) away = transform.right;
            Vector3 side = Vector3.Cross(RingUp.Up(transform.position), away.normalized);
            return _ai.MoveTo(Time.deltaTime, transform.position + side * 2.5f, 0.5f, true);
        }

        public bool TryCast(string statusEffectName, Character target, float duration)
        {
            if (_character == null || string.IsNullOrEmpty(statusEffectName)) return false;
            if (target == null || target == _character) return AbilityRPCManager.ApplySelfBuff(_character, statusEffectName, duration);
            return AbilityRPCManager.ApplySingleEffect(_character, target, statusEffectName, duration);
        }

        public int CollectAllies(float range, List<Character> into)
        {
            if (into == null) return 0;
            into.Clear();
            if (_character == null) return 0;
            ScanBuffer.Clear();
            Character.GetCharactersInRange(transform.position, range, ScanBuffer);
            foreach (var candidate in ScanBuffer)
            {
                if (candidate == null || candidate == _character || candidate.IsDead()) continue;
                if (candidate.IsPlayer() || BaseAI.IsEnemy(_character, candidate)) continue;
                into.Add(candidate);
            }
            return into.Count;
        }

        public bool IsEnemy(Character other) => _character != null && other != null && BaseAI.IsEnemy(_character, other);

        public void Stagger(Vector3 forceDirection)
        {
            if (_character != null) _character.Stagger(forceDirection);
        }
    }
}
