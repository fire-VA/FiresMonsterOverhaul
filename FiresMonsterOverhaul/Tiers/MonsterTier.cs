using UnityEngine;

namespace FiresMonsterOverhaul.Tiers
{
    /// <summary>Doc 21's four tiers, kept on the creature's ZDO as an int ("fmo_tier").</summary>
    public enum MonsterTier { Normal = 0, Veteran = 1, Elite = 2, Champion = 3 }

    public static class MonsterTierInfo
    {
        // Doc 21 "Tiers and affixes": kill XP x1 / x1.5 / x3 / x6.
        private static readonly float[] Xp = { 1f, 1.5f, 3f, 6f };
        private static readonly int[] Affixes = { 0, 0, 1, 2 };

        private static readonly Color VeteranColor = new Color(0.55f, 0.8f, 1f, 1f);
        private static readonly Color EliteColor = new Color(0.78f, 0.55f, 1f, 1f);
        private static readonly Color ChampionColor = new Color(1f, 0.6f, 0.2f, 1f);

        public static float XpMultiplier(MonsterTier tier) => Xp[Mathf.Clamp((int)tier, 0, Xp.Length - 1)];

        /// <summary>Elite: one affix. Champion: two.</summary>
        public static int AffixCount(MonsterTier tier) => Affixes[Mathf.Clamp((int)tier, 0, Affixes.Length - 1)];

        public static Color NameplateColor(MonsterTier tier)
        {
            switch (tier)
            {
                case MonsterTier.Veteran: return VeteranColor;
                case MonsterTier.Elite: return EliteColor;
                case MonsterTier.Champion: return ChampionColor;
                default: return Color.white;
            }
        }
    }
}
