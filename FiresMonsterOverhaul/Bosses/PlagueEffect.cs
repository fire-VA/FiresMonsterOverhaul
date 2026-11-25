using FiresMonsterOverhaul.Abilities;
using FiresMonsterOverhaul.Affixes;
using HarmonyLib;
using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// Bonemass's Plague (doc 22 §3: "Plague stacks — cleanse at 5 or they burst on the group"). Each hit Bonemass lands
    /// on a player adds a stack (refreshing the timer). At Plague Stacks the plague bursts: a telegraphed ring at the
    /// player that hits everyone inside (Blast: each client hits its own player), and the stacks are gone. Counter: Core's
    /// Purify cleanses "FMO_Plague" (registered at load), and the stacks fall off after Plague Seconds without a hit.
    /// Runs on the hit player's own client (players own themselves; ApplyDamage runs there).
    /// </summary>
    public sealed class PlagueEffect : SE_Stats
    {
        public const string EffectName = "FMO_Plague";
        public static readonly int Hash = EffectName.GetStableHashCode();

        public int Stacks;

        public override void Setup(Character character)
        {
            m_ttl = MonsterConfig.PlagueSeconds.Value;
            base.Setup(character);
        }

        public override string GetIconText() => Stacks > 0 ? Stacks.ToString() : base.GetIconText();

        public static void Register(ObjectDB db)
        {
            if (db == null || db.m_StatusEffects == null) return;
            if (db.m_StatusEffects.Exists(s => s != null && s.name == EffectName)) return;
            var se = CreateInstance<PlagueEffect>();
            se.name = EffectName;
            se.m_name = "Plague";
            se.m_tooltip = "Bonemass's plague. More stacks with every hit he lands; at the limit it bursts on everyone near you. Cleanse it.";
            var poison = db.GetStatusEffect("Poison".GetStableHashCode());
            if (poison != null) se.m_icon = poison.m_icon;
            db.m_StatusEffects.Add(se);
            PurifyHook.Register(EffectName);
        }

        /// <summary>A Bonemass hit landed on this (local, owned) player: one more stack, and the burst at the limit.</summary>
        public static void AddStack(Player player)
        {
            var seman = player != null ? player.GetSEMan() : null;
            if (seman == null) return;
            var se = seman.GetStatusEffect(Hash) as PlagueEffect;
            if (se == null)
            {
                se = seman.AddStatusEffect(Hash, true) as PlagueEffect;
                if (se == null) return;
                se.Stacks = 0;
            }
            else se.ResetTime();
            se.Stacks++;
            int limit = Mathf.Max(1, MonsterConfig.PlagueStacks.Value);
            MonsterLog.Debug($"boss: Bonemass plague on {player.GetPlayerName()}: {se.Stacks}/{limit} stack(s)");
            if (se.Stacks < limit) return;

            Vector3 at = player.transform.position;
            float radius = MonsterConfig.PlagueBurstRadius.Value;
            float delay = MonsterConfig.PlagueBurstDelay.Value;
            seman.RemoveStatusEffect(Hash, true);
            Blast.Broadcast(at, radius, delay, -MonsterConfig.PlagueBurstShare.Value, "boss: Bonemass plague burst");
            MonsterLog.Debug($"boss: Bonemass plague burst on {player.GetPlayerName()} at {at:F1} (r {radius:0.#} m in {delay:0.#} s, {MonsterConfig.PlagueBurstShare.Value:P0} of max health to everyone inside)");
        }
    }

    /// <summary>On the hit player's own client: a hit from Bonemass adds a Plague stack.</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    internal static class PlagueStackPatch
    {
        private static void Postfix(Character __instance, HitData hit)
        {
            if (!MonsterConfig.Enabled.Value || !MonsterConfig.Bosses.Value || hit == null) return;
            if (!(__instance is Player player) || player != Player.m_localPlayer || player.IsDead()) return;
            var attacker = hit.GetAttacker();
            if (attacker == null || Utils.GetPrefabName(attacker.gameObject) != "Bonemass") return;
            if (hit.GetTotalDamage() <= 0f) return;
            PlagueEffect.AddStack(player);
        }
    }
}
