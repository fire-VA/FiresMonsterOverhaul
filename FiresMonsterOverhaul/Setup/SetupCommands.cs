using System;
using System.Globalization;

namespace FiresMonsterOverhaul.Setup
{
    /// <summary>
    /// Console commands for the creature rules (cheats: tests and server admins).
    ///   fmo_setup_eval &lt;prefab&gt; [biome=&lt;Name&gt;] [night] [dungeon] [bosslevel=&lt;n&gt;] - what the rules give, no spawn
    ///   fmo_setup_rule &lt;rule line&gt; | clear - add a session rule on this peer (not saved, not synced), or clear them
    ///   fmo_setup_reload - re-read the rules file (on the server this re-syncs every player)
    /// </summary>
    internal static class SetupCommands
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand("fmo_setup_eval", "[prefab] [biome=Name] [night] [dungeon] [bosslevel=n] - Fires Monster Overhaul: what the creature rules give, without spawning", args =>
            {
                if (args.Length < 2) { args.Context?.AddString("usage: fmo_setup_eval <prefab> [biome=Name] [night] [dungeon] [bosslevel=n]"); return; }
                var biome = Heightmap.Biome.Meadows;
                bool night = false, dungeon = false;
                int bosses = CreatureSetup.BossLevel();
                for (int i = 2; i < args.Length; i++)
                {
                    string a = args[i];
                    if (a.Equals("night", StringComparison.OrdinalIgnoreCase)) night = true;
                    else if (a.Equals("dungeon", StringComparison.OrdinalIgnoreCase)) dungeon = true;
                    else if (a.StartsWith("biome=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Enum.TryParse(a.Substring(6), true, out biome)) { args.Context?.AddString($"fmo_setup_eval: unknown biome '{a.Substring(6)}'"); return; }
                    }
                    else if (a.StartsWith("bosslevel=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(a.Substring(10), NumberStyles.Integer, CultureInfo.InvariantCulture, out bosses)) { args.Context?.AddString($"fmo_setup_eval: bad bosslevel '{a.Substring(10)}'"); return; }
                    }
                    else { args.Context?.AddString($"fmo_setup_eval: unknown argument '{a}'"); return; }
                }
                string line = CreatureSetup.Describe(args[1], biome, dungeon, night, bosses);
                MonsterLog.Debug(line);
                args.Context?.AddString(line);
            }, isCheat: true);

            new Terminal.ConsoleCommand("fmo_setup_rule", "[rule line | clear] - Fires Monster Overhaul: add a creature rule for this session on this peer (tests), or clear them", args =>
            {
                string text = args.FullLine.Length > args[0].Length ? args.FullLine.Substring(args[0].Length).Trim() : "";
                if (text.Length == 0) { args.Context?.AddString("usage: fmo_setup_rule <scope>: <setting>=<value> ...   |   fmo_setup_rule clear"); return; }
                string line;
                if (text.Equals("clear", StringComparison.OrdinalIgnoreCase)) line = $"fmo_setup_rule: cleared {CreatureRules.ClearSessionRules()} session rule(s)";
                else if (CreatureRules.AddSessionRule(text, out string error)) line = $"fmo_setup_rule: added '{text}' ({CreatureRules.ActiveCount} active rule(s))";
                else line = $"fmo_setup_rule: refused '{text}': {error}";
                MonsterLog.Debug(line);
                args.Context?.AddString(line);
            }, isCheat: true);

            new Terminal.ConsoleCommand("fmo_setup_reload", "- Fires Monster Overhaul: re-read the creature rules file", args =>
            {
                CreatureRules.LoadLocal();
                string line = $"fmo_setup_reload: {CreatureRules.ActiveCount} active rule(s) after reading {CreatureRules.FileName}";
                MonsterLog.Debug(line);
                args.Context?.AddString(line);
            }, isCheat: true);
        }
    }
}
