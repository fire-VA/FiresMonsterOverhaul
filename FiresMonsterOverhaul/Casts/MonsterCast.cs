using System;
using UnityEngine;

namespace FiresMonsterOverhaul.Casts
{
    /// <summary>
    /// One cast at a time per creature. The owner runs it; the state is mirrored on the ZDO so every viewer can draw
    /// the nameplate bar and the ground ring. A stagger cancels an interruptible cast (CastPatches).
    /// </summary>
    public sealed class MonsterCast : MonoBehaviour
    {
        public static readonly int ZdoSeq = "fmo_cast_seq".GetStableHashCode();
        public static readonly int ZdoName = "fmo_cast_name".GetStableHashCode();
        public static readonly int ZdoDuration = "fmo_cast_dur".GetStableHashCode();
        public static readonly int ZdoInterruptible = "fmo_cast_int".GetStableHashCode();
        public static readonly int ZdoRing = "fmo_cast_ring".GetStableHashCode();     // 0 none, 1 heal, 2 buff, 3 damage
        public static readonly int ZdoRadius = "fmo_cast_radius".GetStableHashCode();
        public static readonly int ZdoActive = "fmo_cast_active".GetStableHashCode();
        public static readonly int ZdoRingAt = "fmo_cast_ring_at".GetStableHashCode();       // ring centre when not the caster
        public static readonly int ZdoRingAtSet = "fmo_cast_ring_at_set".GetStableHashCode();
        public static readonly int ZdoRingSeconds = "fmo_cast_ring_secs".GetStableHashCode(); // ring longer than the cast

        private ZNetView _nview;
        private Character _character;
        private string _name;
        private float _start, _duration;
        private bool _interruptible;
        private bool _holdStill;
        private float _heavy; // stagger damage taken during this cast (AddHeavyHit)
        private Action _onComplete;

        /// <summary>Set by the heal gate while a finished cast lets its attack through.</summary>
        internal bool ReleasingAttack;

        public bool Active => _onComplete != null;
        public string CurrentName => _name;
        public bool Interruptible => _interruptible;
        /// <summary>False for a cast the creature keeps moving through (Mending Aura's pulse).</summary>
        public bool HoldsStill => _holdStill;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _character = GetComponent<Character>();
        }

        /// <param name="ringCenter">Where the ring is drawn (null: around the caster).</param>
        /// <param name="ringSeconds">How long the ring shows (0: the cast's duration).</param>
        public bool Begin(string name, float duration, bool interruptible, int ring, float ringRadius, Action onComplete, bool holdStill = true,
            Vector3? ringCenter = null, float ringSeconds = 0f)
        {
            if (Active || _nview == null || !_nview.IsValid() || !_nview.IsOwner()) return false;
            _name = name;
            _start = Time.time;
            _duration = Mathf.Max(0.1f, duration);
            _interruptible = interruptible;
            _holdStill = holdStill;
            _heavy = 0f;
            _onComplete = onComplete ?? (() => { });

            var zdo = _nview.GetZDO();
            zdo.Set(ZdoName, name);
            zdo.Set(ZdoDuration, _duration);
            zdo.Set(ZdoInterruptible, interruptible);
            zdo.Set(ZdoRing, ring);
            zdo.Set(ZdoRadius, ringRadius);
            zdo.Set(ZdoRingAtSet, ringCenter.HasValue);
            if (ringCenter.HasValue) zdo.Set(ZdoRingAt, ringCenter.Value);
            zdo.Set(ZdoRingSeconds, ringSeconds);
            zdo.Set(ZdoActive, true);
            zdo.Set(ZdoSeq, zdo.GetInt(ZdoSeq) + 1);

            MonsterLog.Debug($"cast start: {Utils.GetPrefabName(gameObject)} {name} ({(interruptible ? "interruptible" : "uninterruptible")}, {_duration:0.0} s) t={Time.time:0.00}");
            return true;
        }

        /// <summary>
        /// From the ApplyDamage prefix (the owner). A creature vanilla never staggers (m_staggerDamageFactor 0: the bosses)
        /// can't be interrupted by a stagger, and Core turns class stuns on a boss into a slow. So its interruptible cast
        /// breaks on heavy hits instead: the hits' vanilla stagger damage during this cast reaching Interrupt Share of its
        /// max health (10-08: Eikthyr's Thunderhead and a channelled heal could not be interrupted at all).
        /// </summary>
        public static void AddHeavyHit(Character c, HitData hit)
        {
            if (c == null || hit == null || c.IsPlayer() || c.m_staggerDamageFactor > 0f) return;
            var cast = c.GetComponent<MonsterCast>();
            if (cast == null || !cast.Active || !cast._interruptible) return;
            float stagger = hit.m_damage.GetTotalStaggerDamage() * hit.m_staggerMultiplier;
            if (stagger <= 0f) return;
            cast._heavy += stagger;
            float need = Mathf.Max(1f, c.GetMaxHealth() * MonsterConfig.BossInterruptShare.Value);
            if (cast._heavy < need)
            {
                MonsterLog.Debug($"cast hit: {Utils.GetPrefabName(c.gameObject)} {cast._name} took {stagger:0.0} stagger ({cast._heavy:0} of {need:0} to interrupt)");
                return;
            }
            cast.Interrupt($"heavy hits ({cast._heavy:0} of {need:0} stagger)");
        }

        /// <summary>Cancels an interruptible cast. Returns true when one was cancelled.</summary>
        public bool Interrupt(string cause)
        {
            if (!Active || !_interruptible) return false;
            MonsterLog.Debug($"cast interrupted: {Utils.GetPrefabName(gameObject)} {_name} by {cause} after {Time.time - _start:0.00} s t={Time.time:0.00}");
            Clear();
            return true;
        }

        private void Clear()
        {
            _onComplete = null;
            if (_nview != null && _nview.IsValid() && _nview.IsOwner()) _nview.GetZDO().Set(ZdoActive, false);
        }

        private void Update()
        {
            if (!Active) return;
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _character == null || _character.IsDead())
            {
                Clear();
                return;
            }
            if (Time.time - _start < _duration) return;

            var done = _onComplete;
            MonsterLog.Debug($"cast complete: {Utils.GetPrefabName(gameObject)} {_name} after {Time.time - _start:0.00} s t={Time.time:0.00} (realtime {Time.realtimeSinceStartup:0.00})");
            Clear();
            try { done(); }
            catch (Exception ex) { MonsterLog.Error($"cast {_name} completion threw: {ex}"); }
        }
    }
}
