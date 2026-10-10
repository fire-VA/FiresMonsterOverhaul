using FiresMonsterOverhaul.Agent;
using FiresMonsterOverhaul.Casts;
using FiresMonsterOverhaul.Threat;
using HarmonyLib;
using UnityEngine;

namespace FiresMonsterOverhaul.Patches
{
    // Every patch class carries a class-level [HarmonyPatch] (PatchAll skips a class without one:
    // Tools\Check-HarmonyClassAttributes.ps1).

    /// <summary>Puts the overhaul's components on Greydwarfs and Draugr as their MonsterAI wakes.</summary>
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.Awake))]
    internal static class AttachPatch
    {
        private static void Postfix(MonsterAI __instance)
        {
            if (!MonsterConfig.Enabled.Value) return;
            // Phase 2: every creature gets a role (auto + defaults + YAML override); the brain still runs only on the
            // families below.
            if (__instance.GetComponent<Roles.MonsterRoleTag>() == null) __instance.gameObject.AddComponent<Roles.MonsterRoleTag>();
            // Phase 3: every creature gets a tier (rolled once on its owner) and the affix runner (idle without affixes).
            if (__instance.GetComponent<Tiers.MonsterTierTag>() == null) __instance.gameObject.AddComponent<Tiers.MonsterTierTag>();
            if (__instance.GetComponent<Affixes.AffixRunner>() == null) __instance.gameObject.AddComponent<Affixes.AffixRunner>();
            if (__instance.GetComponent<Abilities.VeteranAbilities>() == null) __instance.gameObject.AddComponent<Abilities.VeteranAbilities>();
            // Phase 4: every vanilla boss gets its director (phases, new attacks, soft enrage). A boss's spirit add (a copy
            // of it) drops its boss flags first, and a Feeding add walks to its boss.
            var body = __instance.GetComponent<Character>();
            var nv = __instance.m_nview;
            var zdo = nv != null && nv.IsValid() ? nv.GetZDO() : null;
            if (Bosses.SpiritForm.IsSpirit(zdo)) Bosses.SpiritForm.Apply(body);
            if (zdo != null && zdo.GetZDOID(Bosses.BossDirector.ZdoFeederOf) != ZDOID.None && __instance.GetComponent<Bosses.Feeder>() == null)
                __instance.gameObject.AddComponent<Bosses.Feeder>();
            if (body != null && body.IsBoss() && __instance.GetComponent<Bosses.BossDirector>() == null) __instance.gameObject.AddComponent<Bosses.BossDirector>();
            Packs.PackSystem.OnAwake(__instance);
            var kind = MonsterBrain.Classify(Utils.GetPrefabName(__instance.gameObject));
            if (kind == MonsterBrain.Family.None) return;
            bool greydwarf = kind == MonsterBrain.Family.Greydwarf || kind == MonsterBrain.Family.GreydwarfShaman;
            if (greydwarf && !MonsterConfig.Greydwarfs.Value) return;
            if (!greydwarf && !MonsterConfig.Draugr.Value) return;
            var go = __instance.gameObject;
            if (go.GetComponent<MonsterBrain>() != null) return;
            go.AddComponent<MonsterAgent>();
            go.AddComponent<ThreatTable>();
            go.AddComponent<MonsterCast>();
            go.AddComponent<MonsterBrain>();
        }
    }

    /// <summary>
    /// ApplyDamage runs on the victim's owner. For a monster: Stoneskin cuts the hit, then the Shielded shield
    /// absorbs what's left, before vanilla applies it (and the hit's full stagger is put back afterwards); a glowing
    /// Reflective monster sends part of it back; the damage dealt builds threat. For any victim: a Vampiric attacker
    /// heals from what it dealt.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    internal static class DamageThreatPatch
    {
        // A hit the shield absorbs whole still lands this share, so vanilla still counts its stagger.
        private const float MinThrough = 0.01f;

        internal struct State
        {
            public float Health;
            public bool Reduced;
            public bool Stoneskin;
            public float Factor;
            public float StaggerBefore;
            public float TotalBefore;
        }

        private static void Prefix(Character __instance, HitData hit, out State __state)
        {
            __state = default;
            if (__instance == null) return;
            __state.Health = __instance.GetHealth();
            if (hit == null || __instance.IsPlayer()) return;
            MonsterCast.AddHeavyHit(__instance, hit); // a boss's cast breaks on heavy hits (it can't be staggered)
            float total = hit.GetTotalDamage();
            if (total <= 0f) return;
            if (Affixes.AffixRunner.WardHolds(__instance))
            {
                hit.ApplyModifier(0f); // Warded: immune while its totem stands
                MonsterLog.Debug($"affix: {Utils.GetPrefabName(__instance.gameObject)} warded: {total:0.0} damage ignored (break its totem)");
                return;
            }
            if (Bosses.BossDirector.ImmuneNow(__instance))
            {
                hit.ApplyModifier(0f); // Tethered: immune while a Heartwood stands
                MonsterLog.Debug($"boss: {Utils.GetPrefabName(__instance.gameObject)} tethered: {total:0.0} damage ignored (break the Heartwood)");
                return;
            }
            float keep = 1f;
            if (Affixes.AffixRunner.StoneskinHolds(__instance))
            {
                keep = Mathf.Clamp01(1f - MonsterConfig.StoneskinDamageReduction.Value);
                __state.Stoneskin = true;
            }
            float afterStone = total * keep;
            float through = Affixes.AffixRunner.ShieldAbsorb(__instance, afterStone);
            through = Bosses.BossDirector.ShellAbsorb(__instance, through);
            float kept = Affixes.AffixRunner.SpiritShare(__instance, hit, through);
            float factor = kept / total;
            if (factor >= 1f) return;
            factor = Mathf.Max(MinThrough, factor);
            __state.Reduced = true;
            __state.Factor = factor;
            __state.StaggerBefore = __instance.m_staggerDamage;
            __state.TotalBefore = total;
            hit.ApplyModifier(factor);
        }

        private static void Postfix(Character __instance, HitData hit, State __state)
        {
            if (__instance == null || hit == null) return;
            if (__state.Reduced)
                Affixes.AffixRunner.KeepFullStagger(__instance, hit, __state.StaggerBefore, __state.TotalBefore, __state.Health, __state.Factor, __state.Stoneskin);
            var attacker = hit.GetAttacker();
            if (attacker == null || attacker == __instance) return;
            float dealt = Mathf.Max(0f, __state.Health - __instance.GetHealth());
            Affixes.AffixRunner.Vampire(__instance, attacker, dealt);
            if (__instance.IsPlayer()) return;
            Affixes.AffixRunner.Reflect(__instance, attacker, dealt);
            var table = __instance.GetComponent<ThreatTable>();
            if (table == null) return;
            table.Add(attacker, dealt > 0f ? dealt : hit.GetTotalDamage(), "damage");
        }
    }

    /// <summary>
    /// The interrupt rule: anything that staggers a monster cancels its interruptible cast, and cracks Stoneskin.
    /// RPC_Stagger runs on the owner (Character.Stagger routes there), which is where the cast lives.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.RPC_Stagger))]
    internal static class StaggerInterruptPatch
    {
        private static void Postfix(Character __instance)
        {
            if (__instance != null && !__instance.IsPlayer()) Affixes.AffixRunner.CrackStoneskin(__instance, "stagger");
            var cast = __instance != null ? __instance.GetComponent<MonsterCast>() : null;
            if (cast == null || !cast.Active) return;
            if (!cast.Interruptible)
            {
                MonsterLog.Debug($"cast not interrupted: {Utils.GetPrefabName(__instance.gameObject)} {cast.CurrentName} is uninterruptible");
                return;
            }
            if (cast.Interrupt("stagger"))
            {
                // The heal's weapon waits its full AI interval again, so an interrupted heal is a real loss.
                var humanoid = __instance as Humanoid;
                var weapon = humanoid != null ? humanoid.GetCurrentWeapon() : null;
                if (weapon != null) weapon.m_lastAttackTime = Time.time;
            }
        }
    }

    /// <summary>
    /// Class interrupts (Fire 10-07: interrupts = a real stagger + the class interrupt skills). Shield Bash, Earthshaker,
    /// Feint and Pressure Points stagger through vanilla (StaggerInterruptPatch). Hammer of Justice and Shield Bash also
    /// apply Core's ClassStun / ClassKnockdown, which cancel the cast even on a creature that can't be staggered
    /// (m_staggerDamageFactor 0) or is already mid-stagger. Answer from [wishbone] 10-07.
    /// </summary>
    [HarmonyPatch(typeof(SEMan), nameof(SEMan.AddStatusEffect), new[] { typeof(StatusEffect), typeof(bool), typeof(int), typeof(float), typeof(short) })]
    internal static class ClassInterruptEffectPatch
    {
        private static readonly int Stun = "ClassStun".GetStableHashCode();
        private static readonly int Knockdown = "ClassKnockdown".GetStableHashCode();
        // The Mage silence's planned Core SE ([wishbone] 10-07; name to be confirmed when Core ships it).
        private static readonly int Silence = "ClassSilence".GetStableHashCode();
        // Proposed to [wishbone] 10-08: a boss gets a slow instead of a stun or knockdown, so an interrupt skill would also
        // give it this short marker to cancel its cast (bosses can't be staggered). Read now; harmless until Core sends it.
        private static readonly int Interrupt = "ClassInterrupt".GetStableHashCode();

        private static void Postfix(SEMan __instance, StatusEffect statusEffect)
        {
            if (statusEffect == null) return;
            int hash = statusEffect.NameHash();
            string cause = hash == Stun ? "ClassStun" : hash == Knockdown ? "ClassKnockdown" : hash == Silence ? "ClassSilence"
                : hash == Interrupt ? "ClassInterrupt" : null;
            if (cause == null) return;
            var c = __instance.m_character;
            if (c != null && !c.IsPlayer() && hash != Silence) Affixes.AffixRunner.CrackStoneskin(c, cause);
            var cast = c != null ? c.GetComponent<MonsterCast>() : null;
            if (cast == null || !cast.Active) return;
            cast.Interrupt(cause);
        }
    }

    /// <summary>
    /// Vanilla's AI tick is skipped while FMO drives the creature: a hold-still cast stands it still (the Shaman heal,
    /// the war cry, a Champion's rally, the Veteran abilities' wind-ups; Mending Aura's pulse keeps moving), and a
    /// Brute's charge or a Skirmisher's sidestep moves it through vanilla MoveTo. Never on a possessed creature.
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    internal static class HoldWhileCastingPatch
    {
        private static bool Prefix(MonsterAI __instance, float dt, ref bool __result)
        {
            var nv = __instance.m_nview;
            if (nv != null && nv.IsValid() && nv.GetZDO().GetLong(MonsterBrain.ZdoPossessed) != 0L) return true;
            var boss = __instance.GetComponent<Bosses.BossDirector>();
            if (boss != null && boss.OverrideAI(__instance, dt)) { __result = true; return false; }
            var feeder = __instance.GetComponent<Bosses.Feeder>();
            if (feeder != null && feeder.OverrideAI(__instance, dt)) { __result = true; return false; }
            var abilities = __instance.GetComponent<Abilities.VeteranAbilities>();
            if (abilities != null && abilities.OverrideAI(__instance, dt)) { __result = true; return false; }
            var cast = __instance.GetComponent<MonsterCast>();
            if (cast == null || !cast.Active || !cast.HoldsStill) return true;
            __instance.StopMoving();
            var target = __instance.m_targetCreature;
            if (target != null) __instance.LookAt(target.transform.position);
            __result = true;
            return false;
        }
    }

    /// <summary>
    /// Greydwarf Shaman (and any FriendHurt-type heal attack on a Greydwarf): the heal becomes an interruptible
    /// cast. The attack is held back, a bar + Heal ring show for Shaman Heal Cast Seconds, and the vanilla heal
    /// attack fires only if the cast completes. The postfix tells a Veteran Skirmisher it just attacked (Sidestep).
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    internal static class HealCastGatePatch
    {
        private static bool Prefix(Humanoid __instance, Character target, bool secondaryAttack, ref bool __result)
        {
            if (!MonsterConfig.Enabled.Value || !MonsterConfig.Greydwarfs.Value || __instance.IsPlayer()) return true;
            var cast = __instance.GetComponent<MonsterCast>();
            var brain = __instance.GetComponent<MonsterBrain>();
            if (cast == null || brain == null || cast.ReleasingAttack) return true;
            if (brain.Kind != MonsterBrain.Family.GreydwarfShaman && brain.Kind != MonsterBrain.Family.Greydwarf) return true;
            if (brain.IsPossessed()) return true;

            var weapon = __instance.GetCurrentWeapon();
            if (weapon == null || weapon.m_shared.m_aiTargetType != ItemDrop.ItemData.AiTarget.FriendHurt) return true;

            __result = false;
            if (cast.Active) return false;

            var humanoid = __instance;
            var friend = target;
            float radius = weapon.m_shared.m_attack != null && weapon.m_shared.m_attack.m_attackRange > 0f ? weapon.m_shared.m_attack.m_attackRange : 6f;
            cast.Begin("heal", MonsterConfig.ShamanHealCastSeconds.Value, true, 1, radius, () =>
            {
                cast.ReleasingAttack = true;
                try
                {
                    bool ok = humanoid.StartAttack(friend, false);
                    float before = friend != null ? friend.GetHealth() : -1f;
                    MonsterLog.Debug($"heal released: {Utils.GetPrefabName(humanoid.gameObject)} attack {(ok ? "started" : "refused by vanilla")}; friend {(friend != null ? Utils.GetPrefabName(friend.gameObject) : "none")} HP {before:0.0}");
                    // Vanilla's heal lands at the attack's hit (~0.8 s after the wind-up), so the amount is read 3 s on.
                    if (ok && friend != null) cast.StartCoroutine(ReportHeal(humanoid, friend, before));
                }
                finally { cast.ReleasingAttack = false; }
            });
            return false;
        }

        /// <summary>A Veteran Skirmisher darts aside right after an attack it started (hit and run).</summary>
        private static void Postfix(Humanoid __instance, bool __result)
        {
            if (!__result || __instance == null || __instance.IsPlayer()) return;
            var abilities = __instance.GetComponent<Abilities.VeteranAbilities>();
            if (abilities != null) abilities.OnAttackStarted();
        }

        private static System.Collections.IEnumerator ReportHeal(Humanoid healer, Character friend, float before)
        {
            yield return new WaitForSeconds(3f);
            if (friend == null || healer == null) yield break;
            MonsterLog.Debug($"heal result: {Utils.GetPrefabName(healer.gameObject)} -> {Utils.GetPrefabName(friend.gameObject)} HP {before:0.0} -> {friend.GetHealth():0.0} of {friend.GetMaxHealth():0.0} after 3 s");
        }
    }

    /// <summary>
    /// A monster's death on its owner, while its ZDO is still there: Death Burst, Frenzied allies' stacks, a Warded
    /// monster's totem crumbling, a spirit-linked twin being told, and a boss's pattern objects and roots removed.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
    internal static class DeathBurstPatch
    {
        private static void Prefix(Character __instance)
        {
            Affixes.AffixRunner.OnOwnerDeath(__instance);
            Bosses.BossDirector.OnOwnerDeath(__instance);
        }
    }

    /// <summary>Every peer: the Warded affix's totem and the boss fights' props, so their ZDOs resolve everywhere.</summary>
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    internal static class WardTotemPrefabPatch
    {
        private static void Postfix(ZNetScene __instance)
        {
            Affixes.WardTotem.Register(__instance);
            Bosses.BossProps.Register(__instance);
        }
    }

    /// <summary>
    /// The Elder's Entangle roots and the Heartwood saplings break only to players (round 13, 10-08: the Elder's own
    /// attacks broke a player's roots after 1.3 s). RPC_Damage runs on the prop's owner.
    /// </summary>
    [HarmonyPatch(typeof(Destructible), "RPC_Damage")]
    internal static class BossPropDamagePatch
    {
        private static bool Prefix(Destructible __instance, HitData hit)
        {
            if (hit == null) return true;
            string name = Utils.GetPrefabName(__instance.gameObject);
            if (name != Bosses.BossProps.EntangleRoots && name != Bosses.BossProps.Heartwood) return true;
            var attacker = hit.GetAttacker();
            return attacker != null && attacker.IsPlayer();
        }
    }

    /// <summary>Every peer: the Hexer's curse and Entangle's root, so they can be added by name on whichever peer owns the target.</summary>
    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    internal static class HexEffectPatch
    {
        private static void Postfix(ObjectDB __instance)
        {
            Affixes.HexEffect.Register(__instance);
            Bosses.EntangledEffect.Register(__instance);
        }
    }

    /// <summary>
    /// Entangled: the player's own movement input is held (no direction, run or dodge). Attacks, blocks and jumps still go
    /// through, and vanilla still turns the player toward an attack or a block, so a rooted player can break their roots.
    /// (Not the SEMan speed mods: walking and swimming TURN speeds go through those too.)
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
    internal static class EntangledControlsPatch
    {
        private static void Prefix(Player __instance, ref Vector3 movedir, ref bool run, ref bool autoRun, ref bool dodge)
        {
            var seman = __instance.GetSEMan();
            if (seman == null || !seman.HaveStatusEffect(Bosses.EntangledEffect.Hash)) return;
            movedir = Vector3.zero;
            run = false;
            autoRun = false;
            dodge = false;
        }
    }

    /// <summary>
    /// Damage multipliers on attacks by a monster, read from its synced ZDO: the war-cry buff, Enrage and Pack
    /// Leader's inspire.
    /// </summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.GetLevelDamageFactor))]
    internal static class WarCryDamagePatch
    {
        private static void Postfix(Attack __instance, ref float __result)
        {
            var c = __instance.m_character;
            if (c == null || c.IsPlayer()) return;
            if (c.GetComponent<MonsterBrain>() != null) __result *= MonsterBrain.BuffMultiplier(c);
            if (c.GetComponent<Affixes.AffixRunner>() != null)
                __result *= Affixes.AffixRunner.DamageMultiplier(c) * Setup.CreatureSetup.DamageMultiplier(c);
            if (c.IsBoss()) __result *= Bosses.BossDirector.DamageMultiplier(c);
        }
    }

    /// <summary>
    /// Creature setup's health multiplier, from the synced ZDO. Vanilla's SetupMaxHealth is GetMaxHealthBase x level and
    /// runs on the owner (Awake at full health, SetLevel), so every recompute by any owner keeps the multiplier.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.GetMaxHealthBase))]
    internal static class CreatureHealthPatch
    {
        private static void Postfix(Character __instance, ref float __result)
        {
            if (__instance == null || __instance.IsPlayer()) return;
            var nv = __instance.m_nview;
            if (nv == null || !nv.IsValid()) return;
            float m = nv.GetZDO().GetFloat(Setup.CreatureSetup.ZdoHealthMult, 1f);
            if (m > 0f && m != 1f) __result *= m;
        }
    }
}
