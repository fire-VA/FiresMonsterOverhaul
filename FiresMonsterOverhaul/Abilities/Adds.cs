using System;
using System.Collections.Generic;
using FiresMonsterOverhaul.Affixes;
using FiresMonsterOverhaul.Tiers;
using UnityEngine;

namespace FiresMonsterOverhaul.Abilities
{
    /// <summary>
    /// Creatures a monster brings into the fight (Splitting copies, Summoner adds, spirit-link twins, boss adds): no tier
    /// roll, no creature setup of their own (they take their maker's multipliers), no drops, and they start on their
    /// maker's target. Runs on the maker's owner.
    /// </summary>
    internal static class Adds
    {
        private const float ExtraPlayerRange = 40f;
        private static readonly List<Player> Players = new List<Player>();

        /// <param name="prepare">Runs on the new creature before its level, health and alert are set (a boss add's spirit form
        /// must drop the boss flag before vanilla counts it as an active boss on alert).</param>
        public static Character SpawnCopy(Character maker, string prefab, Vector3 at, int level, Quaternion rotation, Action<Character, ZDO> prepare = null)
        {
            var source = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            var makerView = maker != null ? maker.GetComponent<ZNetView>() : null;
            if (source == null || makerView == null || !makerView.IsValid()) return null;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(at, out float y)) at.y = y + 0.1f;
            var go = UnityEngine.Object.Instantiate(source, at, rotation);
            var nv = go.GetComponent<ZNetView>();
            var copy = go.GetComponent<Character>();
            if (nv == null || !nv.IsValid() || copy == null) return null;
            var z = nv.GetZDO();
            var mine = makerView.GetZDO();
            z.Set(MonsterTierTag.ZdoTierSet, true);
            Setup.CreatureSetup.MarkDone(z);
            z.Set(AffixRunner.ZdoNoDrops, true);
            go.GetComponent<CharacterDrop>()?.SetDropsEnabled(false);
            float hp = mine.GetFloat(Setup.CreatureSetup.ZdoHealthMult, 1f);
            float dmg = mine.GetFloat(Setup.CreatureSetup.ZdoDamageMult, 1f);
            if (hp != 1f) z.Set(Setup.CreatureSetup.ZdoHealthMult, hp);
            if (dmg != 1f) z.Set(Setup.CreatureSetup.ZdoDamageMult, dmg);
            prepare?.Invoke(copy, z);
            copy.SetLevel(Mathf.Max(1, level));
            copy.SetHealth(copy.GetMaxHealth());
            var ai = go.GetComponent<MonsterAI>();
            var makerAi = maker.GetComponent<MonsterAI>();
            if (ai != null && makerAi != null && makerAi.m_targetCreature != null)
            {
                ai.m_targetCreature = makerAi.m_targetCreature;
                ai.SetAlerted(true);
            }
            return copy;
        }

        /// <summary>Doc 22 "Adds": the base count, +1 per extra player within 40 m, capped.</summary>
        public static int CountFor(Vector3 at, int baseCount, int max)
        {
            Players.Clear();
            Player.GetPlayersInRange(at, ExtraPlayerRange, Players);
            int extra = Mathf.Max(0, Players.Count - 1);
            return Mathf.Clamp(baseCount + extra, 0, Mathf.Max(0, max));
        }
    }
}
