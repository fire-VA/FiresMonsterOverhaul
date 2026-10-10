using System;
using System.Collections.Generic;

namespace FiresMonsterOverhaul.Affixes
{
    /// <summary>
    /// Doc 21's sixteen behavioural affixes (Molten / Frostbound / Plagued are one, Elemental, with a per-creature
    /// element), kept on the creature's ZDO as a bitmask ("fmo_affixes"). Phase 3 slices 1-4 (32-PHASE3-PLAN.md).
    /// </summary>
    [Flags]
    public enum MonsterAffix
    {
        None = 0,
        MendingAura = 1 << 0,
        Stoneskin = 1 << 1,
        Enrage = 1 << 2,
        PackLeader = 1 << 3,
        DeathBurst = 1 << 4,
        Vampiric = 1 << 5,
        Shielded = 1 << 6,
        Reflective = 1 << 7,
        Frenzied = 1 << 8,
        Splitting = 1 << 9,
        Blink = 1 << 10,
        Warded = 1 << 11,
        Summoner = 1 << 12,
        Hexer = 1 << 13,
        Elemental = 1 << 14,     // Molten / Frostbound / Plagued: the element is picked per creature (ZDO fmo_hazard_elem)
        SpiritLinked = 1 << 15,
    }

    public static class MonsterAffixInfo
    {
        public static readonly MonsterAffix[] All =
        {
            MonsterAffix.MendingAura, MonsterAffix.Stoneskin, MonsterAffix.Enrage, MonsterAffix.PackLeader, MonsterAffix.DeathBurst,
            MonsterAffix.Vampiric, MonsterAffix.Shielded, MonsterAffix.Reflective, MonsterAffix.Frenzied, MonsterAffix.Splitting,
            MonsterAffix.Blink, MonsterAffix.Warded, MonsterAffix.Summoner, MonsterAffix.Hexer, MonsterAffix.Elemental,
            MonsterAffix.SpiritLinked,
        };

        /// <summary>The element names of the Elemental affix, by its ZDO value (0 fire, 1 frost, 2 poison).</summary>
        public static readonly string[] ElementLabels = { "Molten", "Frostbound", "Plagued" };

        public static string Label(MonsterAffix affix)
        {
            switch (affix)
            {
                case MonsterAffix.MendingAura: return "Mending Aura";
                case MonsterAffix.Stoneskin: return "Stoneskin";
                case MonsterAffix.Enrage: return "Enrage";
                case MonsterAffix.PackLeader: return "Pack Leader";
                case MonsterAffix.DeathBurst: return "Death Burst";
                case MonsterAffix.Vampiric: return "Vampiric";
                case MonsterAffix.Shielded: return "Shielded";
                case MonsterAffix.Reflective: return "Reflective";
                case MonsterAffix.Frenzied: return "Frenzied";
                case MonsterAffix.Splitting: return "Splitting";
                case MonsterAffix.Blink: return "Blink";
                case MonsterAffix.Warded: return "Warded";
                case MonsterAffix.Summoner: return "Summoner";
                case MonsterAffix.Hexer: return "Hexer";
                case MonsterAffix.Elemental: return "Elemental";
                case MonsterAffix.SpiritLinked: return "Spirit-linked";
                default: return affix.ToString();
            }
        }

        /// <summary>A Champion's epithet from its first affix ("Grimbark the Mender").</summary>
        public static string Epithet(MonsterAffix affix)
        {
            switch (affix)
            {
                case MonsterAffix.MendingAura: return "the Mender";
                case MonsterAffix.Stoneskin: return "the Unyielding";
                case MonsterAffix.Enrage: return "the Wrathful";
                case MonsterAffix.PackLeader: return "the Warlord";
                case MonsterAffix.DeathBurst: return "the Volatile";
                case MonsterAffix.Vampiric: return "the Bloodthirsty";
                case MonsterAffix.Shielded: return "the Bulwark";
                case MonsterAffix.Reflective: return "the Mirrored";
                case MonsterAffix.Frenzied: return "the Frenzied";
                case MonsterAffix.Splitting: return "the Many";
                case MonsterAffix.Blink: return "the Shifting";
                case MonsterAffix.Warded: return "the Unbroken";
                case MonsterAffix.Summoner: return "the Caller";
                case MonsterAffix.Hexer: return "the Cursebringer";
                case MonsterAffix.Elemental: return "the Scorched";
                case MonsterAffix.SpiritLinked: return "the Twin-souled";
                default: return "the Dread";
            }
        }

        public static bool Enabled(MonsterAffix affix)
        {
            switch (affix)
            {
                case MonsterAffix.MendingAura: return MonsterConfig.MendingAura.Value;
                case MonsterAffix.Stoneskin: return MonsterConfig.Stoneskin.Value;
                case MonsterAffix.Enrage: return MonsterConfig.Enrage.Value;
                case MonsterAffix.PackLeader: return MonsterConfig.PackLeader.Value;
                case MonsterAffix.DeathBurst: return MonsterConfig.DeathBurst.Value;
                case MonsterAffix.Vampiric: return MonsterConfig.Vampiric.Value;
                case MonsterAffix.Shielded: return MonsterConfig.Shielded.Value;
                case MonsterAffix.Reflective: return MonsterConfig.Reflective.Value;
                case MonsterAffix.Frenzied: return MonsterConfig.Frenzied.Value;
                case MonsterAffix.Splitting: return MonsterConfig.Splitting.Value;
                case MonsterAffix.Blink: return MonsterConfig.Blink.Value;
                case MonsterAffix.Warded: return MonsterConfig.Warded.Value;
                case MonsterAffix.Summoner: return MonsterConfig.Summoner.Value;
                case MonsterAffix.Hexer: return MonsterConfig.Hexer.Value;
                case MonsterAffix.Elemental: return MonsterConfig.Elemental.Value;
                case MonsterAffix.SpiritLinked: return MonsterConfig.SpiritLinked.Value;
                default: return false;
            }
        }

        /// <summary>Accepts "MendingAura", "mending", "Stoneskin", "enrage" (case-insensitive, any prefix of a name).</summary>
        public static bool TryParse(string text, out MonsterAffix affix)
        {
            affix = MonsterAffix.None;
            if (string.IsNullOrEmpty(text)) return false;
            string t = text.Replace(" ", "").ToLowerInvariant();
            foreach (var a in All)
            {
                if (a.ToString().ToLowerInvariant().StartsWith(t)) { affix = a; return true; }
            }
            return false;
        }

        public static string Describe(MonsterAffix mask)
        {
            if (mask == MonsterAffix.None) return "none";
            var names = new List<string>();
            foreach (var a in All) if ((mask & a) != 0) names.Add(Label(a));
            return string.Join(", ", names);
        }

        public static IEnumerable<MonsterAffix> Each(MonsterAffix mask)
        {
            foreach (var a in All) if ((mask & a) != 0) yield return a;
        }
    }
}
