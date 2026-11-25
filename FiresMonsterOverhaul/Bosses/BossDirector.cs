using FiresMonsterOverhaul.Abilities;
using FiresMonsterOverhaul.Casts;
using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// Doc 22 §3, "the parts": on every vanilla boss (Character.IsBoss), on its owner, a 0.5 s tick that runs the
    /// boss's script (its phases and new attacks; 34-PHASE4-BOSSES-PLAN.md) and a soft enrage shared by all bosses.
    /// Phases fire once: the fired ones are a bitmask on the ZDO, so a reload or an owner change never repeats one.
    /// The healing patterns (Tethered, Channelled, Feeding, Shelled) are in BossDirector.Patterns.cs.
    /// </summary>
    public sealed partial class BossDirector : MonoBehaviour
    {
        public static readonly int ZdoPhases = "fmo_boss_phases".GetStableHashCode();
        public static readonly int ZdoCombatSince = "fmo_boss_combat_since".GetStableHashCode();
        public static readonly int ZdoEnraged = "fmo_boss_enraged".GetStableHashCode();
        private const float Tick = 0.5f;
        private const long TicksPerSecond = 10000000L;
        private const float CalmSeconds = 30f; // this long out of combat and the enrage clock restarts

        internal ZNetView Nview;
        internal Character Body;
        internal MonsterAI Ai;
        private MonsterCast _cast;
        private BossScript _script;
        private float _nextTick;
        private float _lastInCombat;

        private void Awake()
        {
            Nview = GetComponent<ZNetView>();
            Body = GetComponent<Character>();
            Ai = GetComponent<MonsterAI>();
            _nextTick = Time.time + Random.Range(0f, Tick);
            _script = BossScript.For(Utils.GetPrefabName(gameObject));
            _script?.Bind(this);
            Blast.EnsureRegistered();
            Entangle.EnsureRegistered();
        }

        internal string Prefab => Utils.GetPrefabName(gameObject);
        internal ZDO Zdo => Nview.GetZDO();
        internal Character Target => Ai != null && Ai.m_targetCreature != null && !Ai.m_targetCreature.IsDead() ? Ai.m_targetCreature : null;

        internal MonsterCast Cast()
        {
            if (_cast == null) _cast = GetComponent<MonsterCast>() ?? Patches.HoldWhileCastingPatch.Added(gameObject.AddComponent<MonsterCast>());
            return _cast;
        }

        private void Update()
        {
            long perfT = FiresMonsterOverhaul.Perf.FmoPerf.Start();
            try { UpdateBody(); }
            finally { FiresMonsterOverhaul.Perf.FmoPerf.Stop(FiresMonsterOverhaul.Perf.FmoPerf.Path.BossDirector, perfT); }
        }

        private void UpdateBody()
        {
            if (Time.time < _nextTick || !MonsterConfig.Enabled.Value || !MonsterConfig.Bosses.Value) return;   // 0.0.48: the cheap clock check first
            _nextTick = Time.time + Tick;
            if (Nview == null || !Nview.IsValid() || !Nview.IsOwner() || Body == null || Body.IsDead()) return;
            if (SpiritForm.IsSpirit(Zdo)) { enabled = false; Destroy(this); return; } // a boss's spirit add is no boss
            bool inCombat = Target != null;
            UpdateEnrage(inCombat);
            float max = Body.GetMaxHealth();
            _script?.Tick(inCombat, max > 0f ? Body.GetHealth() / max : 1f);
            TickPattern();
        }

        /// <summary>0.0.40: raised on the owner with the boss's ZDOID and the phase name each time a phase fires (tests only).</summary>
        internal static event System.Action<ZDOID, string> PhaseFired;

        /// <summary>True the first time this phase fires for this boss (then never again).</summary>
        internal bool FirePhase(int bit, string name, float healthFraction)
        {
            var zdo = Zdo;
            int fired = zdo.GetInt(ZdoPhases);
            if ((fired & bit) != 0) return false;
            zdo.Set(ZdoPhases, fired | bit);
            MonsterLog.Debug($"boss: {Prefab} phase {name} at {healthFraction:P0} health {zdo.m_uid}");
            PhaseFired?.Invoke(zdo.m_uid, name);   // 0.0.40: the fmo_boss_phase_test reads the phase line in memory
            return true;
        }

        // Soft enrage (doc 22): more damage after a set time in combat; the clock restarts after a lull.
        private void UpdateEnrage(bool inCombat)
        {
            var zdo = Zdo;
            long now = ZNet.instance.GetTime().Ticks;
            if (inCombat)
            {
                _lastInCombat = Time.time;
                long since = zdo.GetLong(ZdoCombatSince);
                if (since == 0L) { zdo.Set(ZdoCombatSince, now); return; }
                if (zdo.GetBool(ZdoEnraged)) return;
                float seconds = (now - since) / (float)TicksPerSecond;
                if (seconds < MonsterConfig.BossEnrageSeconds.Value) return;
                zdo.Set(ZdoEnraged, true);
                MonsterLog.Debug($"boss: {Prefab} enraged after {seconds:0} s in combat (x{1f + MonsterConfig.BossEnrageDamageBonus.Value:0.00}) {zdo.m_uid}");
                return;
            }
            if (zdo.GetLong(ZdoCombatSince) == 0L || Time.time - _lastInCombat < CalmSeconds) return;
            zdo.Set(ZdoCombatSince, 0L);
            zdo.Set(ZdoEnraged, false);
        }

        /// <summary>The soft enrage's damage multiplier, read from the synced ZDO.</summary>
        public static float DamageMultiplier(Character c)
        {
            if (c == null || !c.IsBoss() || !MonsterConfig.Bosses.Value) return 1f;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.GetZDO().GetBool(ZdoEnraged)) return 1f;
            return 1f + MonsterConfig.BossEnrageDamageBonus.Value;
        }

        /// <summary>From the MonsterAI.UpdateAI prefix: true while the boss's script drives it (a charge).</summary>
        internal bool OverrideAI(MonsterAI ai, float dt) =>
            _script != null && Nview != null && Nview.IsValid() && Nview.IsOwner() && _script.OverrideAI(ai, dt);

        /// <summary>The fmo_cast_ready test hook.</summary>
        public void ResetCooldowns()
        {
            _script?.ResetCooldowns();
            _nextChannel = 0f;
        }

        /// <summary>From the Character.OnDeath prefix, on the boss's owner: its pattern objects and roots go with it.</summary>
        public static void OnOwnerDeath(Character c)
        {
            var director = c != null ? c.GetComponent<BossDirector>() : null;
            if (director == null || director.Nview == null || !director.Nview.IsValid() || !director.Nview.IsOwner()) return;
            director._script?.OnDeath();
            if (director.Pattern != HealPattern.None) director.EndPattern("the boss died", true);
        }
    }

    /// <summary>One vanilla boss's phases and new attacks (doc 22 §3 "Each boss").</summary>
    internal abstract class BossScript
    {
        protected BossDirector D;

        public void Bind(BossDirector director) => D = director;
        public abstract void Tick(bool inCombat, float healthFraction);
        public virtual bool OverrideAI(MonsterAI ai, float dt) => false;
        public virtual void ResetCooldowns() { }
        public virtual void OnDeath() { }

        public static BossScript For(string prefab)
        {
            switch (prefab)
            {
                case "Eikthyr": return new EikthyrScript();
                case "gd_king": return new ElderScript();
                case "Bonemass": return new BonemassScript();
                // 0.0.40 (Fire): the later bosses' phases, on this same system
                case "Dragon": return new ModerScript();
                case "GoblinKing": return new YagluthScript();
                case "SeekerQueen": return new QueenScript();
                case "Fader": return new FaderScript();
                default: return null; // the soft enrage still applies
            }
        }
    }
}
