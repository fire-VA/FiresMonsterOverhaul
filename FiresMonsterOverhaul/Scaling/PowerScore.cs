using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace FiresMonsterOverhaul.Scaling
{
    /// <summary>
    /// A player's power score vs the biome a boss lives in (35-GROUP-SCALING-PLAN.md §2). Computed on the player's OWN client,
    /// which knows its gear for sure, and sent to the boss's owner (GroupScaler.FMO_Power). 1.0 = "geared as the biome expects".
    /// Inputs (each a ratio, 1.0 = expected), weighted by [11 - Boss Scaling] Power Weight *:
    ///   gear      worn armour + the equipped weapon's damage vs the biome's expected (Expected Power Per Biome)
    ///   quality   the average upgrade level of the equipped items / 2
    ///   crafting  Fires Loot Overhaul FloApi.Describe(item) (or the older FiresRPGCrafting FrcApi): 1 + 0.5 x avg rarity share + 0.1 x (affixes + gems) per item; absent = 1
    ///   classes   RPGClasses' "rpg_class_level" / "rpg_class_maxlevel" on the player's ZDO: 0.5 + level/max; absent = 1
    ///   skills    the equipped weapon's skill + Blocking, averaged, vs the biome's expected skill
    /// A missing mod or an unreadable input counts as expected (1.0), never 0, so it never makes a boss easier.
    /// </summary>
    internal static class PowerScore
    {
        private static readonly int ZdoClassLevel = "rpg_class_level".GetStableHashCode();
        private static readonly int ZdoClassMax = "rpg_class_maxlevel".GetStableHashCode();
        private static MethodInfo _frcDescribe;
        private static bool _frcLooked;
        private static string _expectedSource;
        private static Dictionary<Heightmap.Biome, (float armor, float damage, float skill)> _expected;

        public struct Parts { public float Gear, Quality, Crafting, Classes, Skills, Total; public bool HasFrc, HasClasses; }

        public static Parts For(Player p, Heightmap.Biome biome)
        {
            var parts = new Parts { Gear = 1f, Quality = 1f, Crafting = 1f, Classes = 1f, Skills = 1f, Total = 1f };
            if (p == null) return parts;
            var exp = Expected(biome);
            var weapon = p.GetCurrentWeapon();
            float armor = p.GetBodyArmor();
            float damage = weapon != null ? weapon.GetDamage().GetTotalDamage() : 0f;
            if (exp.armor > 0f && exp.damage > 0f) parts.Gear = 0.5f * (armor / exp.armor) + 0.5f * (damage / exp.damage);

            var items = p.GetInventory()?.GetEquippedItems() ?? new List<ItemDrop.ItemData>();
            int counted = 0; float quality = 0f;
            float rarityShare = 0f, extras = 0f; int frcItems = 0;
            foreach (var it in items)
            {
                if (it?.m_shared == null) continue;
                counted++; quality += it.m_quality;
                var d = FrcDescribe(it);
                if (d != null && d.Length >= 4) { parts.HasFrc = true; frcItems++; if (d[1] > 0) rarityShare += d[0] / (float)d[1]; extras += d[2] + d[3]; }
            }
            if (counted > 0) parts.Quality = quality / counted / 2f;
            if (frcItems > 0) parts.Crafting = 1f + 0.5f * (rarityShare / frcItems) + 0.1f * (extras / frcItems);

            var nv = p.GetComponent<ZNetView>();
            if (nv != null && nv.IsValid())
            {
                int level = nv.GetZDO().GetInt(ZdoClassLevel, 0), max = nv.GetZDO().GetInt(ZdoClassMax, 0);
                if (level > 0 && max > 0) { parts.HasClasses = true; parts.Classes = 0.5f + level / (float)max; }
            }

            float ws = weapon != null ? p.GetSkillLevel(weapon.m_shared.m_skillType) : 0f;
            float bl = p.GetSkillLevel(Skills.SkillType.Blocking);
            if (exp.skill > 0f) parts.Skills = Mathf.Max(0.1f, (ws + bl) / 2f / exp.skill);

            float wg = ScalingConfig.WeightGear.Value, wq = ScalingConfig.WeightQuality.Value, wc = ScalingConfig.WeightCrafting.Value,
                  wk = ScalingConfig.WeightClasses.Value, ws2 = ScalingConfig.WeightSkills.Value;
            float sum = wg + wq + wc + wk + ws2;
            if (sum > 0f) parts.Total = (wg * parts.Gear + wq * parts.Quality + wc * parts.Crafting + wk * parts.Classes + ws2 * parts.Skills) / sum;
            parts.Total = Mathf.Clamp(parts.Total, 0.2f, 20f);   // 0.0.31 (Fire 10-14 north star): 5x -> 20x, so a near-OP build still reads as one
            return parts;
        }

        private static int[] FrcDescribe(ItemDrop.ItemData item)
        {
            if (!_frcLooked)
            {
                _frcLooked = true;
                // Fires Loot Overhaul (FiresLootOverhaul.FloApi) is the renamed FiresRPGCrafting (FrcApi), Fire 10-13. Both are accepted so
                // this build works across the rename: the new name, the old name, then any loaded FloApi/FrcApi with the Describe contract.
                var t = Type.GetType("FiresLootOverhaul.FloApi, FiresLootOverhaul") ?? Type.GetType("FiresRPGCrafting.FrcApi, FiresRPGCrafting");
                _frcDescribe = t?.GetMethod("Describe", new[] { typeof(ItemDrop.ItemData) });
                if (_frcDescribe == null)
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type[] types;
                        try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types; } catch { continue; }
                        foreach (var ty in types)
                        {
                            if (ty == null || (ty.Name != "FloApi" && ty.Name != "FrcApi")) continue;
                            var m = ty.GetMethod("Describe", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ItemDrop.ItemData) }, null);
                            if (m != null && m.ReturnType == typeof(int[])) { _frcDescribe = m; break; }
                        }
                        if (_frcDescribe != null) break;
                    }
                MonsterLog.Debug(_frcDescribe != null ? $"power: {_frcDescribe.DeclaringType.FullName}.Describe found ({_frcDescribe.DeclaringType.Assembly.GetName().Name})" : "power: no FloApi/FrcApi.Describe (crafting counts as expected)");
            }
            if (_frcDescribe == null) return null;
            try { return _frcDescribe.Invoke(null, new object[] { item }) as int[]; } catch { return null; }
        }

        public static (float armor, float damage, float skill) Expected(Heightmap.Biome biome)
        {
            string src = ScalingConfig.ExpectedPerBiome.Value ?? "";
            if (_expected == null || src != _expectedSource)
            {
                _expectedSource = src;
                _expected = new Dictionary<Heightmap.Biome, (float, float, float)>();
                foreach (var part in src.Split(','))
                {
                    var kv = part.Split('=');
                    if (kv.Length != 2 || !Enum.TryParse(kv[0].Trim(), true, out Heightmap.Biome b)) continue;
                    var n = kv[1].Split(':');
                    if (n.Length != 3) continue;
                    if (float.TryParse(n[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float a)
                        && float.TryParse(n[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float d)
                        && float.TryParse(n[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float s))
                        _expected[b] = (a, d, s);
                }
            }
            foreach (var pair in _expected) if ((biome & pair.Key) != 0) return pair.Value;
            return _expected.TryGetValue(Heightmap.Biome.Meadows, out var m) ? m : (8f, 18f, 10f);
        }
    }
}
