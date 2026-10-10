using FiresMonsterOverhaul.Affixes;
using FiresMonsterOverhaul.Tiers;

namespace FiresMonsterOverhaul.Api
{
    /// <summary>
    /// Read-only tier and affix data for other mods, e.g. RPGClasses' kill XP (doc 20: x1 / x1.5 / x3 / x6). Everything
    /// is read from the creature's synced ZDO, so it answers on every peer. A mod without a reference to this one can
    /// read the same ZDO ints: "fmo_tier" (0 Normal, 1 Veteran, 2 Elite, 3 Champion) and "fmo_affixes" (a bitmask).
    /// </summary>
    public static class MonsterTiers
    {
        public static MonsterTier GetTier(Character c) => MonsterTierTag.TierOf(Zdo(c));

        public static MonsterAffix GetAffixes(Character c) => MonsterTierTag.AffixesOf(Zdo(c));

        /// <summary>The kill-XP multiplier for this creature's tier (1 when it has none).</summary>
        public static float XpMultiplier(Character c) => MonsterTierInfo.XpMultiplier(GetTier(c));

        /// <summary>A Champion's name ("Grimbark the Mender"), or an empty string.</summary>
        public static string GetChampionName(Character c)
        {
            var zdo = Zdo(c);
            return zdo == null ? "" : zdo.GetString(MonsterTierTag.ZdoChampionName);
        }

        private static ZDO Zdo(Character c)
        {
            if (c == null) return null;
            var nv = c.GetComponent<ZNetView>();
            return nv != null && nv.IsValid() ? nv.GetZDO() : null;
        }
    }
}
