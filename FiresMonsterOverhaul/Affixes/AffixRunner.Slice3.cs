using System.Collections.Generic;
using FiresMonsterOverhaul.Casts;
using FiresMonsterOverhaul.Tiers;
using UnityEngine;

namespace FiresMonsterOverhaul.Affixes
{
    /// <summary>
    /// Phase 3 slice 3 (32-PHASE3-PLAN.md; Fire 10-08: "the next affixes"): Vampiric, Shielded, Reflective, Frenzied,
    /// Splitting, Blink. Owner-side ticks here; the damage-side parts (shield absorb, reflect, vampiric heal) are
    /// called from the Character.ApplyDamage patch. All state other peers or a new owner need is on the ZDO.
    /// </summary>
    public sealed partial class AffixRunner
    {
        public static readonly int ZdoShield = "fmo_shield".GetStableHashCode();
        public static readonly int ZdoShieldHitAt = "fmo_shield_hit".GetStableHashCode();
        public static readonly int ZdoReflectUntil = "fmo_reflect_until".GetStableHashCode();
        public static readonly int ZdoFrenzy = "fmo_frenzy".GetStableHashCode();
        public static readonly int ZdoSplit = "fmo_split".GetStableHashCode();
        public static readonly int ZdoNoDrops = "fmo_nodrops".GetStableHashCode();
        private const string RpcFrenzy = "FMO_Frenzy";
        private const string FrenzyEffectName = "FMO_Frenzy";
        private const float FrenzyEffectSeconds = 3600f; // held while it lives; re-applied when the stacks change
        private const float SplitHealth = 0.5f;
        private const float SplitOffset = 1.5f;
        private const float BlinkBehind = 2f;
        private const float BlinkRingRadius = 1.5f;
        private const float ReflectRingRadius = 2.5f;

        private static SE_Stats _frenzyEffect;
        private static readonly List<Player> Players = new List<Player>();
        private float _nextReflect;
        private float _nextBlink;
        private int _frenzyApplied;

        private void AwakeSlice3()
        {
            if (_nview == null || !_nview.IsValid()) return;
            _nview.Register<int>(RpcFrenzy, RPC_Frenzy);
            // A Splitting copy drops nothing, on every peer that runs its death.
            if (_nview.GetZDO().GetBool(ZdoNoDrops)) GetComponent<CharacterDrop>()?.SetDropsEnabled(false);
        }

        private void UpdateSlice3(ZDO zdo, MonsterAffix affixes, bool inCombat)
        {
            if ((affixes & MonsterAffix.Shielded) != 0 && MonsterConfig.Shielded.Value) UpdateShield(zdo);
            if ((affixes & MonsterAffix.Frenzied) != 0 && MonsterConfig.Frenzied.Value) UpdateFrenzy(zdo);
            if ((affixes & MonsterAffix.Splitting) != 0 && MonsterConfig.Splitting.Value) TrySplit(zdo);
            if (!inCombat) return;
            if ((affixes & MonsterAffix.Reflective) != 0 && MonsterConfig.Reflective.Value) TryReflect(zdo);
            if ((affixes & MonsterAffix.Blink) != 0 && MonsterConfig.Blink.Value) TryBlink();
        }

        private static long Now() => ZNet.instance != null ? ZNet.instance.GetTime().Ticks : 0L;

        // ---------------------------------------------------------------- Shielded

        private float ShieldMax() => _character.GetMaxHealth() * MonsterConfig.ShieldHealth.Value;

        // A fresh shield on the first tick; full again after Shield Recharge Seconds without damage.
        private void UpdateShield(ZDO zdo)
        {
            float max = ShieldMax();
            float shield = zdo.GetFloat(ZdoShield, -1f);
            if (shield < 0f) { zdo.Set(ZdoShield, max); return; }
            if (shield >= max) return;
            long lastHit = zdo.GetLong(ZdoShieldHitAt);
            if ((Now() - lastHit) / (float)TicksPerSecond < MonsterConfig.ShieldRechargeSeconds.Value) return;
            zdo.Set(ZdoShield, max);
            MonsterLog.Debug($"affix: {Utils.GetPrefabName(gameObject)} shield recharged to {max:0} {zdo.m_uid}");
        }

        /// <summary>
        /// On the monster's owner, from the ApplyDamage prefix: how much of <paramref name="incoming"/> gets through
        /// the shield. The shield takes the rest.
        /// </summary>
        public static float ShieldAbsorb(Character c, float incoming)
        {
            if (c == null || c.IsPlayer() || incoming <= 0f || !MonsterConfig.Shielded.Value) return incoming;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return incoming;
            var zdo = nv.GetZDO();
            if ((MonsterTierTag.AffixesOf(zdo) & MonsterAffix.Shielded) == 0) return incoming;
            zdo.Set(ZdoShieldHitAt, Now());
            float shield = zdo.GetFloat(ZdoShield, -1f);
            if (shield <= 0f) return incoming;
            float absorbed = Mathf.Min(shield, incoming);
            float left = shield - absorbed;
            zdo.Set(ZdoShield, left);
            string prefab = Utils.GetPrefabName(c.gameObject);
            if (left <= 0f) MonsterLog.Debug($"affix: {prefab} shield broken (absorbed {absorbed:0.0} of {incoming:0.0}) {zdo.m_uid}");
            else MonsterLog.Debug($"affix: {prefab} shield absorbed {absorbed:0.0} of {incoming:0.0} ({left:0.0} left) {zdo.m_uid}");
            return incoming - absorbed;
        }

        public static float ShieldLeft(ZDO zdo) => zdo.GetFloat(ZdoShield, -1f);

        // ---------------------------------------------------------------- Reflective

        // A glow with a telegraph (grey bar + ring) every Reflect Cooldown; hits taken during it are partly reflected.
        private void TryReflect(ZDO zdo)
        {
            if (Time.time < _nextReflect) return;
            if (_cast == null) _cast = GetComponent<MonsterCast>() ?? Patches.HoldWhileCastingPatch.Added(gameObject.AddComponent<MonsterCast>());
            if (_cast.Active) return; // the glow always shows its bar; wait for a free cast slot
            float seconds = MonsterConfig.ReflectSeconds.Value;
            if (!_cast.Begin("reflect", seconds, false, 2, ReflectRingRadius, () => { }, holdStill: false)) return;
            _nextReflect = Time.time + MonsterConfig.ReflectCooldown.Value;
            zdo.Set(ZdoReflectUntil, Now() + (long)(seconds * TicksPerSecond));
            MonsterLog.Debug($"affix: {Utils.GetPrefabName(gameObject)} reflect glow for {seconds:0.#} s ({MonsterConfig.ReflectShare.Value:P0} of each hit back) {zdo.m_uid}");
        }

        public static bool Reflecting(ZDO zdo) => zdo.GetLong(ZdoReflectUntil) > Now();

        /// <summary>
        /// On the monster's owner, from the ApplyDamage postfix: during the glow, a player's (or a tame's) hit comes
        /// partly back at them. The monster still takes the hit.
        /// </summary>
        public static void Reflect(Character c, Character attacker, float dealt)
        {
            if (c == null || attacker == null || attacker == c || dealt <= 0f || !MonsterConfig.Reflective.Value) return;
            if (!(attacker.IsPlayer() || attacker.IsTamed()) || attacker.IsDead()) return;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return;
            var zdo = nv.GetZDO();
            if ((MonsterTierTag.AffixesOf(zdo) & MonsterAffix.Reflective) == 0 || !Reflecting(zdo)) return;
            float back = dealt * MonsterConfig.ReflectShare.Value;
            var hit = new HitData();
            hit.m_damage.m_damage = back;
            hit.m_point = attacker.GetCenterPoint();
            hit.m_dir = (attacker.transform.position - c.transform.position).normalized;
            hit.m_hitType = HitData.HitType.EnemyHit;
            hit.SetAttacker(c);
            attacker.Damage(hit);
            MonsterLog.Debug($"affix: {Utils.GetPrefabName(c.gameObject)} reflected {back:0.0} of {dealt:0.0} back at {attacker.GetHoverName()} {zdo.m_uid}");
        }

        // ---------------------------------------------------------------- Vampiric

        /// <summary>
        /// From the ApplyDamage postfix on the VICTIM's owner: a Vampiric attacker heals a share of what it dealt
        /// (Character.Heal routes to the attacker's owner).
        /// </summary>
        public static void Vampire(Character victim, Character attacker, float dealt)
        {
            if (attacker == null || victim == null || attacker == victim || dealt <= 0f || !MonsterConfig.Vampiric.Value) return;
            if (attacker.IsPlayer() || attacker.IsDead()) return;
            var nv = attacker.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            if ((MonsterTierTag.AffixesOf(nv.GetZDO()) & MonsterAffix.Vampiric) == 0) return;
            float heal = dealt * MonsterConfig.VampiricHeal.Value;
            if (heal <= 0f) return;
            attacker.Heal(heal, true);
            MonsterLog.Debug($"affix: {Utils.GetPrefabName(attacker.gameObject)} vampiric +{heal:0.0} HP from hitting {victim.GetHoverName()} for {dealt:0.0} {nv.GetZDO().m_uid}");
        }

        // ---------------------------------------------------------------- Frenzied

        /// <summary>From the death prefix on the dying creature's owner: Frenzied allies nearby gain a stack.</summary>
        public static void NotifyFrenzy(Character dead)
        {
            if (dead == null || dead.IsPlayer() || !MonsterConfig.Frenzied.Value) return;
            Scan.Clear();
            Character.GetCharactersInRange(dead.transform.position, MonsterConfig.FrenzyRadius.Value, Scan);
            foreach (var c in Scan)
            {
                if (c == null || c == dead || c.IsDead() || c.IsPlayer() || BaseAI.IsEnemy(dead, c)) continue;
                var nv = c.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || (MonsterTierTag.AffixesOf(nv.GetZDO()) & MonsterAffix.Frenzied) == 0) continue;
                if (c.GetComponent<AffixRunner>() == null) continue;
                nv.InvokeRPC(RpcFrenzy, 1);
            }
        }

        private void RPC_Frenzy(long sender, int add)
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _character == null || _character.IsDead()) return;
            var zdo = _nview.GetZDO();
            int stacks = Mathf.Min(MonsterConfig.FrenzyMaxStacks.Value, zdo.GetInt(ZdoFrenzy) + add);
            zdo.Set(ZdoFrenzy, stacks);
            MonsterLog.Debug($"affix: {Utils.GetPrefabName(gameObject)} frenzy x{stacks} (an ally died nearby; +{stacks * MonsterConfig.FrenzySpeedPerAlly.Value:P0} speed) {zdo.m_uid}");
            UpdateFrenzy(zdo);
        }

        // The speed rides on a status effect on the owner (movement is simulated there); re-applied when the stacks
        // change or the owner changes.
        private void UpdateFrenzy(ZDO zdo)
        {
            int stacks = zdo.GetInt(ZdoFrenzy);
            var seman = _character.GetSEMan();
            bool has = seman.HaveStatusEffect(FrenzyEffectName.GetStableHashCode());
            if (stacks == _frenzyApplied && (stacks == 0 || has)) return;
            if (has) seman.RemoveStatusEffect(FrenzyEffectName.GetStableHashCode(), true);
            _frenzyApplied = stacks;
            if (stacks <= 0) return;
            if (_frenzyEffect == null)
            {
                _frenzyEffect = ScriptableObject.CreateInstance<SE_Stats>();
                _frenzyEffect.name = FrenzyEffectName;
                _frenzyEffect.m_name = FrenzyEffectName;
            }
            _frenzyEffect.m_ttl = FrenzyEffectSeconds;
            _frenzyEffect.m_speedModifier = stacks * MonsterConfig.FrenzySpeedPerAlly.Value;
            seman.AddStatusEffect(_frenzyEffect, true);
        }

        public static int FrenzyStacks(ZDO zdo) => zdo.GetInt(ZdoFrenzy);

        // ---------------------------------------------------------------- Splitting

        // Once, at half health: a copy appears beside it with the same health (no tier, no affixes, no drops).
        private void TrySplit(ZDO zdo)
        {
            if (zdo.GetBool(ZdoSplit)) return;
            float max = _character.GetMaxHealth();
            if (max <= 0f || _character.GetHealth() > max * SplitHealth) return;
            zdo.Set(ZdoSplit, true);
            string prefab = Utils.GetPrefabName(gameObject);
            var source = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            if (source == null) return;
            Vector3 p = transform.position + transform.right * SplitOffset;
            var go = Object.Instantiate(source, p, transform.rotation);
            var nv = go.GetComponent<ZNetView>();
            var copy = go.GetComponent<Character>();
            if (nv == null || !nv.IsValid() || copy == null) return;
            var z = nv.GetZDO();
            z.Set(MonsterTierTag.ZdoTierSet, true);
            Setup.CreatureSetup.MarkDone(z);
            z.Set(ZdoNoDrops, true);
            go.GetComponent<CharacterDrop>()?.SetDropsEnabled(false);
            float hpMult = zdo.GetFloat(Setup.CreatureSetup.ZdoHealthMult, 1f);
            float dmgMult = zdo.GetFloat(Setup.CreatureSetup.ZdoDamageMult, 1f);
            if (hpMult != 1f) z.Set(Setup.CreatureSetup.ZdoHealthMult, hpMult);
            if (dmgMult != 1f) z.Set(Setup.CreatureSetup.ZdoDamageMult, dmgMult);
            copy.SetLevel(_character.GetLevel());
            copy.SetHealth(_character.GetHealth());
            var ai = go.GetComponent<MonsterAI>();
            if (ai != null && _ai != null && _ai.m_targetCreature != null)
            {
                ai.m_targetCreature = _ai.m_targetCreature;
                ai.SetAlerted(true);
            }
            MonsterLog.Debug($"affix: {prefab} split at {_character.GetHealth():0} HP: + {prefab}={z.m_uid} (no tier, no affixes, no drops) {zdo.m_uid}");
        }

        // ---------------------------------------------------------------- Blink

        // The farthest player it can see who is not its current target (the backline); its target when alone.
        private Player PickBlinkPlayer()
        {
            Players.Clear();
            Player.GetPlayersInRange(transform.position, MonsterConfig.BlinkRange.Value, Players);
            var current = _ai != null ? _ai.m_targetCreature as Player : null;
            Player best = null;
            float bestDistance = -1f;
            foreach (var p in Players)
            {
                if (p == null || p.IsDead() || p == current) continue;
                if (_ai != null && !_ai.CanSeeTarget(p)) continue;
                float d = Vector3.Distance(transform.position, p.transform.position);
                if (d > bestDistance) { bestDistance = d; best = p; }
            }
            return best ?? (current != null && !current.IsDead() ? current : null);
        }

        private static Vector3 Behind(Player p) => p.transform.position - p.transform.forward * BlinkBehind;

        // A short interruptible cast with a ring where it will appear, then a teleport behind that player.
        private void TryBlink()
        {
            if (Time.time < _nextBlink) return;
            if (_cast == null) _cast = GetComponent<MonsterCast>() ?? Patches.HoldWhileCastingPatch.Added(gameObject.AddComponent<MonsterCast>());
            if (_cast.Active) return;
            var target = PickBlinkPlayer();
            if (target == null) return;
            float seconds = MonsterConfig.BlinkCastSeconds.Value;
            Vector3 dest = Behind(target);
            if (!_cast.Begin("blink", seconds, true, 2, BlinkRingRadius, () => DoBlink(target), holdStill: true, ringCenter: dest)) return;
            _nextBlink = Time.time + MonsterConfig.BlinkCooldown.Value;
            MonsterLog.Debug($"affix: {Utils.GetPrefabName(gameObject)} blink cast toward {target.GetPlayerName()} ({Vector3.Distance(transform.position, target.transform.position):0.0} m, {seconds:0.0} s) {_nview.GetZDO().m_uid}");
        }

        private void DoBlink(Player target)
        {
            if (target == null || target.IsDead() || _character == null || _character.IsDead()) return;
            Vector3 from = transform.position;
            Vector3 dest = Behind(target);
            transform.position = dest;
            if (_character.m_body != null)
            {
                _character.m_body.position = dest;
                _character.m_body.velocity = Vector3.zero;
            }
            Vector3 look = target.transform.position - dest;
            look = RingUp.Flat(look, dest);   // 0.0.46: halo ring plane
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look, RingUp.Up(dest));
            if (_ai != null) _ai.m_targetCreature = target;
            MonsterLog.Debug($"affix: {Utils.GetPrefabName(gameObject)} blinked behind {target.GetPlayerName()} ({Vector3.Distance(from, dest):0.0} m) {_nview.GetZDO().m_uid}");
        }
    }
}
