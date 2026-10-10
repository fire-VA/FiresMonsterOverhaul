using System.Collections.Generic;
using FiresMonsterOverhaul.Agent;
using FiresMonsterOverhaul.Casts;
using FiresMonsterOverhaul.Tiers;
using UnityEngine;

namespace FiresMonsterOverhaul.Affixes
{
    /// <summary>
    /// The affixes' behaviour on the creature's owner, on a 0.5 s tick: Mending Aura's pulse cast, Enrage's combat
    /// clock, Pack Leader's inspire pulse and the Champion's half-health rally. Stoneskin lives in the damage and
    /// stagger patches and Death Burst in the death patch (MonsterPatches), through the helpers below. Every state
    /// other peers or a new owner need is on the ZDO.
    /// </summary>
    public sealed partial class AffixRunner : MonoBehaviour
    {
        public static readonly int ZdoStoneCrackedUntil = "fmo_stone_cracked".GetStableHashCode();
        public static readonly int ZdoCombatSince = "fmo_combat_since".GetStableHashCode();
        public static readonly int ZdoEnraged = "fmo_enraged".GetStableHashCode();
        public static readonly int ZdoInspireMult = "fmo_inspire_mult".GetStableHashCode();
        public static readonly int ZdoInspireUntil = "fmo_inspire_until".GetStableHashCode();
        public static readonly int ZdoChampionPhase = "fmo_champ_phase".GetStableHashCode();
        private const string RpcInspire = "FMO_Inspire";
        private const float Tick = 0.5f;
        private const long TicksPerSecond = 10000000L;
        private const float InspirePulseSeconds = 2f;
        private const float InspireSeconds = 3f;
        private const float ChampionPhaseHealth = 0.5f;
        private const float RallyRingRadius = 8f;

        private static readonly List<Character> Scan = new List<Character>();
        private static readonly List<Character> Hurt = new List<Character>();

        private ZNetView _nview;
        private Character _character;
        private MonsterAI _ai;
        private MonsterCast _cast;
        private float _nextTick;
        private float _nextPulse;
        private float _lastInCombat;
        private float _nextInspire;
        private int _lastInspiredLogged = -1;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _character = GetComponent<Character>();
            _ai = GetComponent<MonsterAI>();
            _nextTick = Time.time + Random.Range(0f, Tick);
            if (_nview != null && _nview.IsValid()) _nview.Register<float, float>(RpcInspire, RPC_Inspire);
            Abilities.Blast.EnsureRegistered();
            AwakeSlice3();
            AwakeSlice4();
        }

        private void Update()
        {
            if (!MonsterConfig.Enabled.Value || Time.time < _nextTick) return;
            _nextTick = Time.time + Tick;
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _character == null || _character.IsDead()) return;
            var zdo = _nview.GetZDO();
            ReviveIfDue(zdo); // a spirit-linked twin has no affixes of its own but brings its partner back
            var affixes = MonsterTierTag.AffixesOf(zdo);
            if (affixes == MonsterAffix.None || !MonsterTierTag.Eligible(_character, _nview)) return;

            bool inCombat = _ai != null && _ai.m_targetCreature != null && !_ai.m_targetCreature.IsDead();
            if (MonsterTierTag.TierOf(zdo) == MonsterTier.Champion && MonsterConfig.ChampionPhase.Value) TryChampionPhase(zdo);
            if ((affixes & MonsterAffix.Enrage) != 0 && MonsterConfig.Enrage.Value) UpdateEnrage(zdo, inCombat);
            if ((affixes & MonsterAffix.MendingAura) != 0 && MonsterConfig.MendingAura.Value && inCombat) TryMendingPulse();
            if ((affixes & MonsterAffix.PackLeader) != 0 && MonsterConfig.PackLeader.Value && inCombat) TryInspire();
            UpdateSlice3(zdo, affixes, inCombat);
            UpdateSlice4(zdo, affixes, inCombat);
        }

        // Champion (doc 21: "a phase at half health"): once, at half health, an uninterruptible rally it stands still
        // for (a burst window), then one more affix.
        private void TryChampionPhase(ZDO zdo)
        {
            if (zdo.GetBool(ZdoChampionPhase)) return;
            float max = _character.GetMaxHealth();
            if (max <= 0f || _character.GetHealth() > max * ChampionPhaseHealth) return;
            if (_cast == null) _cast = GetComponent<MonsterCast>() ?? gameObject.AddComponent<MonsterCast>();
            if (_cast.Active) return; // after the current cast ends or is interrupted
            float seconds = MonsterConfig.ChampionPhaseCastSeconds.Value;
            bool begun = _cast.Begin("rally", seconds, false, 2, RallyRingRadius, () =>
            {
                var z = _nview.GetZDO();
                var have = MonsterTierTag.AffixesOf(z);
                var extra = MonsterTierTag.PickAffixes(_character, 1, have);
                if (extra == MonsterAffix.None)
                {
                    MonsterLog.Debug($"affix: {Utils.GetPrefabName(gameObject)} champion rallied: no affix left to gain {z.m_uid}");
                    return;
                }
                z.Set(MonsterTierTag.ZdoAffixes, (int)(have | extra));
                MonsterLog.Debug($"affix: {Utils.GetPrefabName(gameObject)} champion rallied: gained {MonsterAffixInfo.Label(extra)} -> [{MonsterAffixInfo.Describe(have | extra)}] {z.m_uid}");
            }, holdStill: true);
            if (!begun) return;
            zdo.Set(ZdoChampionPhase, true);
            MonsterLog.Debug($"affix: {Utils.GetPrefabName(gameObject)} champion phase at {_character.GetHealth() / max:P0} health: rally ({seconds:0.0} s, uninterruptible) {zdo.m_uid}");
        }

        // Pack Leader: while it fights, allies within range hit harder; refreshed every couple of seconds.
        private void TryInspire()
        {
            if (Time.time < _nextInspire) return;
            _nextInspire = Time.time + InspirePulseSeconds;
            float mult = 1f + MonsterConfig.PackLeaderDamageBonus.Value;
            Scan.Clear();
            Character.GetCharactersInRange(transform.position, MonsterConfig.PackLeaderRadius.Value, Scan);
            int inspired = 0;
            foreach (var c in Scan)
            {
                if (c == null || c == _character || c.IsDead() || c.IsPlayer() || BaseAI.IsEnemy(_character, c)) continue;
                if (c.GetComponent<AffixRunner>() == null) continue;
                var nv = c.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                nv.InvokeRPC(RpcInspire, mult, InspireSeconds);
                inspired++;
            }
            if (inspired == _lastInspiredLogged) return;
            _lastInspiredLogged = inspired;
            MonsterLog.Debug($"affix: {Utils.GetPrefabName(gameObject)} pack leader inspires {inspired} ally(ies) (x{mult:0.00}) {_nview.GetZDO().m_uid}");
        }

        /// <summary>
        /// The fmo_cast_ready test hook: every affix timer may go again at once (Mending Aura, Pack Leader, Reflective,
        /// Blink, Summoner, Hexer, the Elemental hazard).
        /// </summary>
        public void ResetCooldowns()
        {
            _nextPulse = 0f;
            _nextInspire = 0f;
            _nextReflect = 0f;
            _nextBlink = 0f;
            _nextSummon = 0f;
            _nextHex = 0f;
            _nextHazard = 0f;
        }

        private void RPC_Inspire(long sender, float mult, float seconds)
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || ZNet.instance == null) return;
            var zdo = _nview.GetZDO();
            zdo.Set(ZdoInspireMult, mult);
            zdo.Set(ZdoInspireUntil, ZNet.instance.GetTime().Ticks + (long)(seconds * TicksPerSecond));
        }

        public static bool Inspired(ZDO zdo) => ZNet.instance != null && zdo.GetLong(ZdoInspireUntil) > ZNet.instance.GetTime().Ticks;

        // Any monster's death on its owner: Frenzied allies nearby gain a stack. Death Burst: the owner announces it as
        // the monster dies; every peer draws the ring, and each client detonates on its own player (players are owned
        // by their own client), so nobody needs the dead creature.
        public static void OnOwnerDeath(Character c)
        {
            if (c == null || c.IsPlayer()) return;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
            NotifyFrenzy(c);
            var zdo = nv.GetZDO();
            OnDeathSlice4(c, zdo);
            if (!MonsterConfig.DeathBurst.Value || ZRoutedRpc.instance == null) return;
            if ((MonsterTierTag.AffixesOf(zdo) & MonsterAffix.DeathBurst) == 0) return;
            Vector3 pos = c.transform.position;
            float radius = MonsterConfig.DeathBurstRadius.Value;
            float delay = MonsterConfig.DeathBurstDelay.Value;
            float damage = c.GetMaxHealth() * MonsterConfig.DeathBurstDamage.Value;
            MonsterLog.Debug($"affix: {Utils.GetPrefabName(c.gameObject)} death burst at {pos:F1} (r {radius:0.#} m in {delay:0.#} s, {damage:0.#} damage) {zdo.m_uid}");
            Abilities.Blast.Broadcast(pos, radius, delay, damage, "affix: death burst");
        }

        // Enrage: more damage after Enrage After Seconds in combat, until it has been out of combat for a while.
        private void UpdateEnrage(ZDO zdo, bool inCombat)
        {
            long now = ZNet.instance.GetTime().Ticks;
            if (inCombat)
            {
                _lastInCombat = Time.time;
                long since = zdo.GetLong(ZdoCombatSince);
                if (since == 0L) { zdo.Set(ZdoCombatSince, now); return; }
                if (zdo.GetBool(ZdoEnraged)) return;
                float seconds = (now - since) / (float)TicksPerSecond;
                if (seconds < MonsterConfig.EnrageAfterSeconds.Value) return;
                zdo.Set(ZdoEnraged, true);
                MonsterLog.Debug($"affix: {Utils.GetPrefabName(gameObject)} enraged after {seconds:0} s in combat (x{1f + MonsterConfig.EnrageDamageBonus.Value:0.00}) {zdo.m_uid}");
                return;
            }
            if (zdo.GetLong(ZdoCombatSince) == 0L || Time.time - _lastInCombat < MonsterConfig.EnrageCalmSeconds.Value) return;
            bool was = zdo.GetBool(ZdoEnraged);
            zdo.Set(ZdoCombatSince, 0L);
            zdo.Set(ZdoEnraged, false);
            if (was) MonsterLog.Debug($"affix: {Utils.GetPrefabName(gameObject)} calmed down (out of combat {MonsterConfig.EnrageCalmSeconds.Value:0} s) {zdo.m_uid}");
        }

        // Mending Aura: an interruptible pulse that heals hurt allies nearby; the caster keeps moving while it casts.
        private void TryMendingPulse()
        {
            if (Time.time < _nextPulse) return;
            if (_cast == null) _cast = GetComponent<MonsterCast>() ?? gameObject.AddComponent<MonsterCast>();
            if (_cast.Active) return;
            float radius = MonsterConfig.MendingAuraRadius.Value;
            if (CollectHurtAllies(radius) == 0) return;
            float castSeconds = MonsterConfig.MendingAuraCastSeconds.Value;
            _nextPulse = Time.time + castSeconds + MonsterConfig.MendingAuraCooldown.Value;
            _cast.Begin("mending aura", castSeconds, true, 1, radius, () =>
            {
                int healed = 0;
                float total = 0f;
                CollectHurtAllies(radius);
                foreach (var ally in Hurt)
                {
                    float amount = ally.GetMaxHealth() * MonsterConfig.MendingAuraHeal.Value;
                    ally.Heal(amount, true);
                    healed++;
                    total += amount;
                }
                MonsterLog.Debug($"affix: {Utils.GetPrefabName(gameObject)} mending aura pulse healed {healed} ally(ies) (+{total:0.0} HP) {_nview.GetZDO().m_uid}");
            }, holdStill: false);
        }

        private int CollectHurtAllies(float radius)
        {
            Hurt.Clear();
            Scan.Clear();
            Character.GetCharactersInRange(transform.position, radius, Scan);
            foreach (var c in Scan)
            {
                if (c == null || c == _character || c.IsDead() || c.IsPlayer()) continue;
                if (BaseAI.IsEnemy(_character, c)) continue;
                if (c.GetHealth() >= c.GetMaxHealth() - 0.5f) continue;
                Hurt.Add(c);
            }
            return Hurt.Count;
        }

        /// <summary>
        /// Enrage and Pack Leader's inspire as one damage multiplier, read from the synced ZDO (the attack runs on the
        /// attacker's owner).
        /// </summary>
        public static float DamageMultiplier(Character c)
        {
            if (c == null) return 1f;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return 1f;
            var zdo = nv.GetZDO();
            float m = 1f;
            if (MonsterConfig.Enrage.Value && (MonsterTierTag.AffixesOf(zdo) & MonsterAffix.Enrage) != 0 && zdo.GetBool(ZdoEnraged))
                m *= 1f + MonsterConfig.EnrageDamageBonus.Value;
            if (Inspired(zdo))
            {
                float inspire = zdo.GetFloat(ZdoInspireMult, 1f);
                if (inspire > 0f) m *= inspire;
            }
            return m;
        }

        /// <summary>True while Stoneskin cuts this creature's damage (it has the affix and isn't cracked). Owner side.</summary>
        public static bool StoneskinHolds(Character c)
        {
            if (c == null || c.IsPlayer() || !MonsterConfig.Stoneskin.Value || ZNet.instance == null) return false;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return false;
            var zdo = nv.GetZDO();
            if ((MonsterTierTag.AffixesOf(zdo) & MonsterAffix.Stoneskin) == 0 || c.IsTamed()) return false;
            return zdo.GetLong(ZdoStoneCrackedUntil) <= ZNet.instance.GetTime().Ticks;
        }

        /// <summary>
        /// Stoneskin and the Shielded shield cut health loss only: vanilla adds stagger from the reduced hit, so the rest
        /// of the hit's FULL stagger is added here (keeping "stagger it" as Stoneskin's counter). <paramref name="factor"/>
        /// is the share of the hit that got through (Stoneskin's keep x the shield's).
        /// </summary>
        public static void KeepFullStagger(Character c, HitData hit, float staggerBefore, float totalBefore, float healthBefore, float factor, bool stoneskin)
        {
            if (c == null || hit == null || c.IsDead()) return;
            float threshold = c.GetStaggerTreshold();
            float vanillaAdded = Mathf.Max(0f, c.m_staggerDamage - staggerBefore);
            float full = factor > 0.0001f ? hit.m_damage.GetTotalStaggerDamage() * hit.m_staggerMultiplier / factor : 0f;
            float missing = full - vanillaAdded;
            bool staggered = threshold > 0f && c.m_staggerDamage >= threshold - 0.001f;
            if (!staggered && missing > 0f) c.AddStaggerDamage(missing, hit.m_dir, hit);
            if (stoneskin)
                MonsterLog.Debug($"affix: {Utils.GetPrefabName(c.gameObject)} stoneskin cut {totalBefore:0.0} -> {Mathf.Max(0f, healthBefore - c.GetHealth()):0.0} (stagger kept {full:0.0} of threshold {threshold:0.0})");
        }

        /// <summary>A stagger or a class stun cracks Stoneskin for Stoneskin Cracked Seconds. Owner side.</summary>
        public static void CrackStoneskin(Character c, string cause)
        {
            if (c == null || ZNet.instance == null || !MonsterConfig.Stoneskin.Value) return;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
            var zdo = nv.GetZDO();
            if ((MonsterTierTag.AffixesOf(zdo) & MonsterAffix.Stoneskin) == 0) return;
            long now = ZNet.instance.GetTime().Ticks;
            if (zdo.GetLong(ZdoStoneCrackedUntil) > now) return;
            float seconds = MonsterConfig.StoneskinCrackedSeconds.Value;
            zdo.Set(ZdoStoneCrackedUntil, now + (long)(seconds * TicksPerSecond));
            MonsterLog.Debug($"affix: {Utils.GetPrefabName(c.gameObject)} stoneskin cracked by {cause} for {seconds:0} s {zdo.m_uid}");
        }

        public static bool StoneskinCracked(ZDO zdo) => ZNet.instance != null && zdo.GetLong(ZdoStoneCrackedUntil) > ZNet.instance.GetTime().Ticks;
    }
}
