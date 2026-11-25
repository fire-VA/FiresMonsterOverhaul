using FiresCore.Npc.Archetypes.Effects;
using FiresMonsterOverhaul.Casts;
using FiresMonsterOverhaul.Threat;
using UnityEngine;

namespace FiresMonsterOverhaul.Agent
{
    /// <summary>
    /// The 0.5 s decision tick, owner peer only, inside Brain Radius of a player. Vanilla MonsterAI still walks and
    /// swings; the brain layers threat-based targeting and the family abilities on top.
    /// It never drives a possessed creature: possession ([ghost], Valcast / Core Shared\FiresPossession) sets the
    /// synced ZDO "valcast_possessed" and turns BaseAI.enabled off on the owner, and either one stops the brain.
    /// </summary>
    public sealed class MonsterBrain : MonoBehaviour
    {
        public enum Family { None, Greydwarf, GreydwarfShaman, Draugr, DraugrElite }

        public static readonly int ZdoPossessed = "valcast_possessed".GetStableHashCode();
        public static readonly int ZdoBuffUntil = "fmo_buff_until".GetStableHashCode();
        public static readonly int ZdoBuffMult = "fmo_buff_mult".GetStableHashCode();
        private const string RpcBuff = "FMO_Buff";
        private const float Tick = 0.5f;

        public Family Kind { get; private set; }

        private MonsterAgent _agent;
        private ThreatTable _threat;
        private MonsterCast _cast;
        private ZNetView _nview;
        private float _nextTick;
        private float _nextWarCry;
        private bool _wasPossessed;

        public static Family Classify(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return Family.None;
            if (prefab == "Greydwarf_Shaman") return Family.GreydwarfShaman;
            if (prefab.StartsWith("Greydwarf")) return Family.Greydwarf;
            if (prefab == "Draugr_Elite") return Family.DraugrElite;
            if (prefab.StartsWith("Draugr")) return Family.Draugr;
            return Family.None;
        }

        private void Awake()
        {
            Kind = Classify(Utils.GetPrefabName(gameObject));
            _agent = GetComponent<MonsterAgent>();
            _threat = GetComponent<ThreatTable>();
            _cast = GetComponent<MonsterCast>();
            _nview = GetComponent<ZNetView>();
            _nextTick = Time.time + Random.Range(0f, Tick);
            _nextWarCry = Time.time + 3f;
            if (_nview != null && _nview.IsValid())
            {
                _nview.Register<float, float>(RpcBuff, RPC_Buff);
                _nview.Register<ZDOID, float>(RpcThreat, RPC_Threat);
            }
        }

        public const string RpcThreat = "FMO_Threat";

        // Threat raised on another peer (a heal lands on the healed player's own client) reaches the owner here.
        private void RPC_Threat(long sender, ZDOID source, float amount)
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _threat == null) return;
            _threat.AddById(source, amount);
            MonsterLog.Debug($"threat: {Utils.GetPrefabName(gameObject)} +{amount:0.#} from heal by {source}");
        }

        public bool IsPossessed()
        {
            if (_nview == null || !_nview.IsValid()) return false;
            if (_nview.GetZDO().GetLong(ZdoPossessed) != 0L) return true;
            return _agent != null && _agent.Ai != null && !_agent.Ai.enabled;
        }

        /// <summary>The fmo_cast_ready test hook: the war cry may go again at once.</summary>
        public void ResetCooldowns() => _nextWarCry = 0f;

        /// <summary>True while the brain owns this creature's decisions this frame (casts hold it still).</summary>
        public bool Holding => _cast != null && _cast.Active && _cast.HoldsStill && !IsPossessed();

        private void Update()
        {
            long perfT = FiresMonsterOverhaul.Perf.FmoPerf.Start();
            try { UpdateBody(); }
            finally { FiresMonsterOverhaul.Perf.FmoPerf.Stop(FiresMonsterOverhaul.Perf.FmoPerf.Path.MonsterBrain, perfT); }
        }

        private void UpdateBody()
        {
            if (Time.time < _nextTick || !MonsterConfig.Enabled.Value) return;   // 0.0.48: the cheap clock check first (the config read was every frame)
            _nextTick = Time.time + Tick;
            if (_agent == null || !_agent.IsOwner || !_agent.IsAlive) return;

            if (IsPossessed())
            {
                if (!_wasPossessed) { _wasPossessed = true; _threat?.Forget("possessed"); }
                return;
            }
            if (_wasPossessed) { _wasPossessed = false; _threat?.Forget("released from possession"); }

            if (!NearPlayer(MonsterConfig.BrainRadius.Value)) return;

            var ai = _agent.MonsterAi;
            bool inCombat = ai != null && ai.m_targetCreature != null;
            _threat?.DecayIfIdle(inCombat, MonsterConfig.ThreatDecaySeconds.Value);
            UpdateTarget();

            if (Kind == Family.DraugrElite && inCombat) TryWarCry();
        }

        private bool NearPlayer(float radius)
        {
            var p = Player.GetClosestPlayer(transform.position, radius);
            return p != null;
        }

        private void UpdateTarget()
        {
            if (_threat == null || _threat.Count == 0) return;
            var ai = _agent.MonsterAi;
            if (ai == null) return;
            var top = _threat.Top(transform.position, 40f, out float topThreat);
            if (top == null || !_agent.IsEnemy(top)) return;
            var current = ai.m_targetCreature;
            if (current == top) { WriteTarget(top); return; }
            // Threat only switches BETWEEN targets. When vanilla has dropped its target (lost sight, fleeing, a Shaman
            // backing off to heal) it re-acquires through its own perception; forcing one back every tick fought it
            // (monsters r3: "target: Greydwarf_Shaman none -> Vafire" every 0.5 s, and the Shaman stopped healing).
            if (current == null || current.IsDead()) { WriteTarget(null); return; }
            if (!ai.CanSeeTarget(top)) return;

            float currentThreat = current != null && !current.IsDead() ? _threat.Of(current) : 0f;
            bool melee = (top.transform.position - transform.position).sqrMagnitude <= MonsterConfig.MeleeRange.Value * MonsterConfig.MeleeRange.Value;
            float margin = melee ? MonsterConfig.ThreatSwitchMelee.Value : MonsterConfig.ThreatSwitchRanged.Value;
            if (current != null && !current.IsDead() && topThreat <= currentThreat * (1f + margin)) return;

            MonsterLog.Debug($"target: {Utils.GetPrefabName(gameObject)} {(current != null ? current.GetHoverName() : "none")} -> {top.GetHoverName()} (threat {currentThreat:0.#} vs {topThreat:0.#}, margin {margin:P0}{(melee ? " melee" : " ranged")})");
            ai.m_targetCreature = top;
            WriteTarget(top);
        }

        private void WriteTarget(Character target)
        {
            var zdo = _nview.GetZDO();
            ZDOID id = target != null ? target.GetZDOID() : ZDOID.None;
            if (zdo.GetZDOID(ThreatTable.ZdoTarget) != id) zdo.Set(ThreatTable.ZdoTarget, id);
        }

        // Draugr Elite: an interruptible war cry, then a damage buff on Draugr nearby.
        private void TryWarCry()
        {
            if (!MonsterConfig.Draugr.Value || _cast == null || _cast.Active || Time.time < _nextWarCry) return;
            _nextWarCry = Time.time + MonsterConfig.WarCryCooldown.Value;
            float radius = MonsterConfig.WarCryRadius.Value;
            _cast.Begin("war cry", MonsterConfig.WarCryCastSeconds.Value, true, 2, radius, () =>
            {
                float mult = 1f + MonsterConfig.WarCryDamageBonus.Value;
                float seconds = MonsterConfig.WarCryDuration.Value;
                int buffed = 0;
                var allies = new System.Collections.Generic.List<Character>();
                _agent.CollectAllies(radius, allies);
                allies.Add(_agent.Body);
                foreach (var ally in allies)
                {
                    var brain = ally.GetComponent<MonsterBrain>();
                    if (brain == null || (brain.Kind != Family.Draugr && brain.Kind != Family.DraugrElite)) continue;
                    var nv = ally.GetComponent<ZNetView>();
                    if (nv == null || !nv.IsValid()) continue;
                    nv.InvokeRPC(RpcBuff, mult, seconds);
                    buffed++;
                }
                MonsterLog.Debug($"war cry: {Utils.GetPrefabName(gameObject)} buffed {buffed} Draugr x{mult:0.00} for {seconds:0} s");
            });
        }

        private void RPC_Buff(long sender, float mult, float seconds)
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            var zdo = _nview.GetZDO();
            zdo.Set(ZdoBuffMult, mult);
            zdo.Set(ZdoBuffUntil, ZNet.instance.GetTime().Ticks + (long)(seconds * 10000000L));
        }

        /// <summary>The damage multiplier from an active war-cry buff, read from the synced ZDO.</summary>
        public static float BuffMultiplier(Character c)
        {
            if (c == null || ZNet.instance == null) return 1f;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return 1f;
            var zdo = nv.GetZDO();
            if (zdo.GetLong(ZdoBuffUntil) <= ZNet.instance.GetTime().Ticks) return 1f;
            float m = zdo.GetFloat(ZdoBuffMult, 1f);
            return m > 0f ? m : 1f;
        }

        /// <summary>The ring kind a cast's ZDO names, for the per-viewer telegraph.</summary>
        public static bool TryRingKind(int code, out GroundRingKind kind)
        {
            kind = GroundRingKind.Damage;
            switch (code)
            {
                case 1: kind = GroundRingKind.Heal; return true;
                case 2: kind = GroundRingKind.Buff; return true;
                case 3: kind = GroundRingKind.Damage; return true;
                default: return false;
            }
        }
    }
}
