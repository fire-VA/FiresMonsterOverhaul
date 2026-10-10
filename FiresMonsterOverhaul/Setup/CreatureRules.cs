using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using FiresCore.Sync;

namespace FiresMonsterOverhaul.Setup
{
    /// <summary>
    /// The creature setup rules (33-CREATURE-SETUP-PLAN.md; Fire 10-07: CLLC-style options with FMO's own logic, a
    /// neutral default). One rule per line in BepInEx\config\FiresMonsterOverhaul.creatures.txt:
    /// "&lt;scope&gt;: &lt;setting&gt;=&lt;value&gt; ...". The server's text is synced to every peer (the owner of a
    /// monster applies it, and that can be a client). A test can add session rules with fmo_setup_rule.
    /// </summary>
    internal static class CreatureRules
    {
        public const string FileName = "FiresMonsterOverhaul.creatures.txt";
        private const string SyncId = "creature rules";

        internal enum Scope { All, Biome, Creature, Dungeon, Night, BossLevel }

        internal sealed class Rule
        {
            public Scope Scope;
            public string Arg = "";
            public float Health = 1f;
            public float Damage = 1f;
            public float Star1;
            public float Star2;
            public string Text = "";
        }

        internal struct Result
        {
            public float Health;
            public float Damage;
            public float NightDamage;
            public float Star1;
            public float Star2;
            public string Why;
            public bool Neutral => Health == 1f && Damage == 1f && Star1 <= 0f && Star2 <= 0f;
        }

        // Fire 10-07, option (a): a fresh install plays like vanilla. Every rule ships commented out.
        private const string Template =
@"# Fires Monster Overhaul - creature setup (CLLC-style options, FMO's own logic).
# One rule per line:  <scope>: <setting>=<value> ...      Lines starting with # are ignored.
#
# Scopes:
#   all                - every monster
#   biome <Name>       - Meadows, BlackForest, Swamp, Mountain, Plains, Mistlands, AshLands, DeepNorth, Ocean
#   creature <Prefab>  - one creature, e.g. creature Troll
#   dungeon            - inside a dungeon or crypt
#   night              - health and stars when it appears at night; damage while it is night
#   bosslevel          - per boss defeated (0-7, Eikthyr to Fader): health=+10% adds 10 % for each boss
# Settings:
#   health, damage     - multipliers: 1.2, 120% or +20% all mean x1.2
#   star1, star2       - extra chance to be a 1-star / 2-star monster: 5% or 0.05
# Multipliers from every matching line multiply; star chances add up. A monster is never lowered below the
# stars vanilla gave it. Bosses, tamed creatures and possessed creatures are never changed.
# With [1 - General] Lock Configuration on, the SERVER's file applies to every player.
#
# The shipped file is neutral: nothing below is active. Remove the leading # from a line to use it.
#
# all: health=1.0 damage=1.0
#
# A gentle curve, the world hardening as bosses fall:
# bosslevel: health=+10% damage=+5% star1=+2%
# night: damage=1.1
#
# More examples:
# biome Swamp: health=1.2 damage=1.1 star1=10% star2=2%
# creature Troll: health=1.25
# dungeon: health=1.2 damage=1.1
";

        private static readonly List<Rule> FileRules = new List<Rule>();
        private static readonly List<Rule> SessionRules = new List<Rule>();
        private static CustomSyncedValue<string> _synced;
        private static string _localText = "";

        public static string FilePath => Path.Combine(Paths.ConfigPath, FileName);

        /// <summary>Reads (or writes the template for) the local file. The server's copy is what gets synced.</summary>
        public static void LoadLocal()
        {
            try
            {
                if (!File.Exists(FilePath)) File.WriteAllText(FilePath, Template, new UTF8Encoding(false));
                _localText = File.ReadAllText(FilePath);
            }
            catch (Exception ex)
            {
                MonsterLog.Warning($"creature rules: could not read {FileName}: {ex.Message}");
                _localText = "";
            }
            if (_synced != null) _synced.AssignLocalValue(_localText);
            else Parse(_localText, "local file");
        }

        public static void BindToSync(ConfigSync sync)
        {
            _synced = new CustomSyncedValue<string>(sync, SyncId, _localText);
            _synced.ValueChanged += () => Parse(_synced.Value ?? "", "synced");
            Parse(_synced.Value ?? "", "synced");
        }

        private static void Parse(string text, string source)
        {
            FileRules.Clear();
            int lineNo = 0, bad = 0;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                lineNo++;
                if (!TryParseLine(raw, out var rule, out string error))
                {
                    if (error == null) continue;
                    bad++;
                    MonsterLog.Warning($"creature rules: line {lineNo} skipped ({error}): {raw.Trim()}");
                    continue;
                }
                FileRules.Add(rule);
            }
            MonsterLog.Debug($"creature rules: {FileRules.Count} active rule(s) from the {source} text ({bad} skipped)");
        }

        /// <summary>False with a null error for a blank or comment line; false with an error for a bad one.</summary>
        internal static bool TryParseLine(string raw, out Rule rule, out string error)
        {
            rule = null;
            error = null;
            string line = raw ?? "";
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line.Substring(0, hash);
            line = line.Trim();
            if (line.Length == 0) return false;

            int colon = line.IndexOf(':');
            if (colon < 0) { error = "no ':' after the scope"; return false; }
            string[] scopeWords = line.Substring(0, colon).Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (scopeWords.Length == 0) { error = "no scope"; return false; }
            var r = new Rule { Text = line };
            string arg = scopeWords.Length > 1 ? string.Join("", scopeWords, 1, scopeWords.Length - 1) : "";
            switch (scopeWords[0].ToLowerInvariant())
            {
                case "all": r.Scope = Scope.All; break;
                case "biome":
                    if (!Enum.TryParse(arg, true, out Heightmap.Biome biome) || biome == Heightmap.Biome.None) { error = $"unknown biome '{arg}'"; return false; }
                    r.Scope = Scope.Biome; r.Arg = biome.ToString(); break;
                case "creature":
                    if (arg.Length == 0) { error = "creature needs a prefab name"; return false; }
                    r.Scope = Scope.Creature; r.Arg = arg; break;
                case "dungeon": r.Scope = Scope.Dungeon; break;
                case "night": r.Scope = Scope.Night; break;
                case "bosslevel": r.Scope = Scope.BossLevel; break;
                default: error = $"unknown scope '{scopeWords[0]}'"; return false;
            }

            foreach (var setting in line.Substring(colon + 1).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = setting.IndexOf('=');
                if (eq <= 0) { error = $"'{setting}' is not setting=value"; return false; }
                string key = setting.Substring(0, eq).ToLowerInvariant();
                string value = setting.Substring(eq + 1);
                switch (key)
                {
                    case "health": if (!TryMultiplier(value, out r.Health)) { error = $"bad health '{value}'"; return false; } break;
                    case "damage": if (!TryMultiplier(value, out r.Damage)) { error = $"bad damage '{value}'"; return false; } break;
                    case "star1": if (!TryChance(value, out r.Star1)) { error = $"bad star1 '{value}'"; return false; } break;
                    case "star2": if (!TryChance(value, out r.Star2)) { error = $"bad star2 '{value}'"; return false; } break;
                    default: error = $"unknown setting '{key}'"; return false;
                }
            }
            rule = r;
            return true;
        }

        // 1.2, 120% and +20% all mean x1.2.
        private static bool TryMultiplier(string text, out float value)
        {
            value = 1f;
            string t = text.Trim();
            bool relative = t.StartsWith("+") || t.StartsWith("-");
            bool percent = t.EndsWith("%");
            if (percent) t = t.Substring(0, t.Length - 1);
            if (!float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return false;
            if (percent) v /= 100f;
            value = relative ? 1f + v : v;
            return value > 0f;
        }

        // 5% or 0.05.
        private static bool TryChance(string text, out float value)
        {
            value = 0f;
            string t = text.Trim().TrimStart('+');
            bool percent = t.EndsWith("%");
            if (percent) t = t.Substring(0, t.Length - 1);
            if (!float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return false;
            value = percent ? v / 100f : v;
            return value >= 0f && value <= 1f;
        }

        /// <summary>
        /// What the rules give one monster. Night's damage is returned separately (applied live while it is night);
        /// a bosslevel rule counts once per boss defeated (+10% each).
        /// </summary>
        public static Result Evaluate(string prefab, Heightmap.Biome biome, bool dungeon, bool night, int bossLevel)
        {
            var r = new Result { Health = 1f, Damage = 1f, NightDamage = 1f };
            var why = new List<string>();
            foreach (var list in new[] { FileRules, SessionRules })
            {
                foreach (var rule in list)
                {
                    switch (rule.Scope)
                    {
                        case Scope.All: break;
                        case Scope.Biome: if (!string.Equals(rule.Arg, biome.ToString(), StringComparison.OrdinalIgnoreCase)) continue; break;
                        case Scope.Creature: if (!string.Equals(rule.Arg, prefab, StringComparison.OrdinalIgnoreCase)) continue; break;
                        case Scope.Dungeon: if (!dungeon) continue; break;
                        case Scope.Night:
                            if (!night) { r.NightDamage *= rule.Damage; continue; }
                            break;
                        case Scope.BossLevel: if (bossLevel <= 0) continue; break;
                    }
                    if (rule.Scope == Scope.BossLevel)
                    {
                        r.Health *= 1f + (rule.Health - 1f) * bossLevel;
                        r.Damage *= 1f + (rule.Damage - 1f) * bossLevel;
                        r.Star1 += rule.Star1 * bossLevel;
                        r.Star2 += rule.Star2 * bossLevel;
                        why.Add($"bosslevel {bossLevel}");
                        continue;
                    }
                    r.Health *= rule.Health;
                    if (rule.Scope == Scope.Night) r.NightDamage *= rule.Damage;
                    else r.Damage *= rule.Damage;
                    r.Star1 += rule.Star1;
                    r.Star2 += rule.Star2;
                    why.Add(rule.Scope == Scope.Biome || rule.Scope == Scope.Creature ? $"{rule.Scope.ToString().ToLowerInvariant()} {rule.Arg}" : rule.Scope.ToString().ToLowerInvariant());
                }
            }
            r.Star1 = Math.Min(1f, r.Star1);
            r.Star2 = Math.Min(1f - r.Star1, r.Star2);
            r.Why = why.Count == 0 ? "no rule" : string.Join(", ", why);
            return r;
        }

        /// <summary>Night's live damage factor (every matching night rule), for the damage patch.</summary>
        public static float NightDamage()
        {
            float m = 1f;
            foreach (var list in new[] { FileRules, SessionRules })
                foreach (var rule in list)
                    if (rule.Scope == Scope.Night) m *= rule.Damage;
            return m;
        }

        public static bool AddSessionRule(string line, out string error)
        {
            if (!TryParseLine(line, out var rule, out error)) { error = error ?? "empty rule"; return false; }
            SessionRules.Add(rule);
            return true;
        }

        public static int ClearSessionRules()
        {
            int n = SessionRules.Count;
            SessionRules.Clear();
            return n;
        }

        public static int ActiveCount => FileRules.Count + SessionRules.Count;
    }
}
