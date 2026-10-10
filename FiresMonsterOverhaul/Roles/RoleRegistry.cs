using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace FiresMonsterOverhaul.Roles
{
    /// <summary>
    /// Who plays which role (doc 21 "Roles and packs", doc 22 "Who gets what"). Order of precedence:
    /// 1. the YAML override file BepInEx\config\FiresMonsterOverhaul.roles.yaml ("Prefab: Role" lines, # comments);
    /// 2. the built-in defaults for vanilla creatures (doc 22's table);
    /// 3. automatic assignment from the creature's data (heal weapon, ranged weapon, speed, health), so modded
    ///    creatures fit too.
    /// The override file lives inside the game folder (public-mod rule: no I/O outside it).
    /// </summary>
    internal static class RoleRegistry
    {
        public const string FileName = "FiresMonsterOverhaul.roles.yaml";

        // Doc 22's humanoid table, vanilla prefab names. Per-instance creatures whose weapon varies (Skeleton, Draugr)
        // are left to the automatic pass, which reads the weapon each one actually carries.
        private static readonly Dictionary<string, MonsterRole> Defaults = new Dictionary<string, MonsterRole>(StringComparer.Ordinal)
        {
            // Meadows (Fire 10-07: Meadows + Black Forest first; Deer stay solo, they're AnimalAI)
            { "Greyling", MonsterRole.Skirmisher },
            { "Boar", MonsterRole.Brute },
            { "Neck", MonsterRole.Ambusher },
            // Black Forest and beyond
            { "Greydwarf", MonsterRole.Skirmisher },
            { "Greydwarf_Elite", MonsterRole.Brute },
            { "Greydwarf_Shaman", MonsterRole.Shaman },
            { "Draugr_Elite", MonsterRole.Warlord },
            { "Goblin", MonsterRole.Skirmisher },
            { "GoblinBrute", MonsterRole.Brute },
            { "GoblinShaman", MonsterRole.Shaman },
            { "Charred_Melee", MonsterRole.Brute },
            { "Charred_Archer", MonsterRole.Caster },
            { "Charred_Mage", MonsterRole.Summoner },
            { "DvergerMage", MonsterRole.Caster },
            { "DvergerMageSupport", MonsterRole.Shaman },
            { "Troll", MonsterRole.Brute },
        };

        private static Dictionary<string, MonsterRole> _overrides;

        public static string FilePath => Path.Combine(Paths.ConfigPath, FileName);

        public static void Load()
        {
            _overrides = new Dictionary<string, MonsterRole>(StringComparer.Ordinal);
            try
            {
                if (!File.Exists(FilePath)) { File.WriteAllText(FilePath, Template); return; }
                int lineNo = 0;
                foreach (var raw in File.ReadAllLines(FilePath))
                {
                    lineNo++;
                    var line = raw;
                    int hash = line.IndexOf('#');
                    if (hash >= 0) line = line.Substring(0, hash);
                    line = line.Trim();
                    if (line.Length == 0) continue;
                    int colon = line.IndexOf(':');
                    if (colon <= 0) { MonsterLog.Warning($"{FileName} line {lineNo}: expected 'Prefab: Role', got '{raw.Trim()}'"); continue; }
                    string prefab = line.Substring(0, colon).Trim().Trim('"', '\'');
                    string role = line.Substring(colon + 1).Trim().Trim('"', '\'');
                    if (!Enum.TryParse(role, true, out MonsterRole parsed))
                    {
                        MonsterLog.Warning($"{FileName} line {lineNo}: '{role}' is not a role (Brute, Skirmisher, Shaman, Caster, Summoner, Warlord, Ambusher, Bombardier, None)");
                        continue;
                    }
                    _overrides[prefab] = parsed;
                }
                if (_overrides.Count > 0) MonsterLog.Info($"{FileName}: {_overrides.Count} role override(s)");
            }
            catch (Exception ex)
            {
                MonsterLog.Warning($"{FileName} could not be read ({ex.Message}); using the built-in roles");
            }
        }

        /// <summary>The role for this creature, and why (for the Debug evidence line).</summary>
        public static MonsterRole Resolve(Character c, out string why)
        {
            string prefab = Utils.GetPrefabName(c.gameObject);
            if (_overrides == null) Load();
            if (_overrides.TryGetValue(prefab, out var o)) { why = "override"; return o; }
            if (c.IsBoss()) { why = "boss"; return MonsterRole.None; }
            if (Defaults.TryGetValue(prefab, out var d)) { why = "default"; return d; }
            return Auto(c, out why);
        }

        private static MonsterRole Auto(Character c, out string why)
        {
            var weapon = (c as Humanoid)?.GetCurrentWeapon();
            var humanoid = c as Humanoid;
            if (humanoid != null)
            {
                foreach (var item in humanoid.GetInventory()?.GetAllItems() ?? new List<ItemDrop.ItemData>())
                {
                    if (item?.m_shared == null) continue;
                    if (item.m_shared.m_aiTargetType == ItemDrop.ItemData.AiTarget.FriendHurt) { why = "auto: heals allies"; return MonsterRole.Shaman; }
                }
            }
            var attack = weapon?.m_shared?.m_attack;
            if (attack != null && attack.m_attackType == Attack.AttackType.Projectile)
            {
                bool lobbed = attack.m_projectileVel > 0f && attack.m_projectileVel < 15f;
                if (lobbed) { why = "auto: lobbed projectile"; return MonsterRole.Bombardier; }
                why = "auto: ranged weapon"; return MonsterRole.Caster;
            }
            if (c.m_runSpeed >= 8f) { why = "auto: fast"; return MonsterRole.Skirmisher; }
            if (c.GetMaxHealth() >= 400f) { why = "auto: heavy"; return MonsterRole.Brute; }
            why = "auto: no role";
            return MonsterRole.None;
        }

        private const string Template =
"# Fires Monster Overhaul - role overrides. One 'Prefab: Role' per line; # starts a comment.\n" +
"# Roles: Brute, Skirmisher, Shaman, Caster, Summoner, Warlord, Ambusher, Bombardier, None.\n" +
"# A creature listed here gets this role instead of the built-in or automatic one (modded creatures too).\n" +
"# Examples:\n" +
"# Greydwarf_Elite: Brute\n" +
"# Draugr: Brute\n" +
"# Skeleton_Poison: Bombardier\n";
    }
}
