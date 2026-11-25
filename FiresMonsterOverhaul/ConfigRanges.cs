using BepInEx.Configuration;

namespace FiresMonsterOverhaul
{
    /// <summary>
    /// Binds a config entry with a sane AcceptableValueRange for every number that has none (config audit 10-13), chosen
    /// by what the key measures: chances/shares/fractions 0-1, seconds 0-3600, metres 0-500, counts and multipliers
    /// 0 to max(10, 10 x default). An explicit range in the description always wins.
    /// </summary>
    internal static class ConfigRanges
    {
        public static ConfigEntry<T> Bind<T>(ConfigFile cfg, string section, string key, T value, string description)
            => Bind(cfg, section, key, value, new ConfigDescription(description));

        public static ConfigEntry<T> Bind<T>(ConfigFile cfg, string section, string key, T value, ConfigDescription d)
        {
            if (d.AcceptableValues == null)
            {
                AcceptableValueBase range = null;
                if (value is float f) { var (lo, hi) = Span(key, f); range = new AcceptableValueRange<float>(lo, hi); }
                else if (value is int i) { var (lo, hi) = Span(key, i); range = new AcceptableValueRange<int>((int)lo, (int)System.Math.Ceiling(hi)); }
                if (range != null) d = new ConfigDescription(d.Description, range, d.Tags);
            }
            return cfg.Bind(section, key, value, d);
        }

        /// <summary>
        /// One-time migration for the zero-padded section names (0.0.23): a value still under the old "N - X" section is copied
        /// into "0N - X" (logged), and the old orphan removed, so nobody's settings reset.
        /// </summary>
        public static void MigratePaddedSections(ConfigFile cfg)
        {
            var prop = typeof(ConfigFile).GetProperty("OrphanedEntries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (!(prop?.GetValue(cfg) is System.Collections.Generic.Dictionary<ConfigDefinition, string> orphans) || orphans.Count == 0) return;
            int moved = 0;
            foreach (var def in new System.Collections.Generic.List<ConfigDefinition>(cfg.Keys))
            {
                var m = System.Text.RegularExpressions.Regex.Match(def.Section, @"^0(\d) - (.+)$");
                if (!m.Success) continue;
                var old = new ConfigDefinition($"{m.Groups[1].Value} - {m.Groups[2].Value}", def.Key);
                if (!orphans.TryGetValue(old, out string raw)) continue;
                cfg[def].SetSerializedValue(raw);
                orphans.Remove(old);
                moved++;
                MonsterLog.Info($"config: moved [{old.Section}] {def.Key} = {raw} to [{def.Section}]");
            }
            if (moved > 0) { cfg.Save(); MonsterLog.Info($"config: {moved} value(s) moved to the zero-padded sections; the old ones removed"); }
        }

        /// <summary>
        /// A changed default reaches users who never touched the setting: a saved value still equal to the OLD default moves to the
        /// new one (logged and saved); a value the user changed stays theirs. (A user who deliberately typed the old default gets
        /// the new one too; the cfg can't tell those apart.)
        /// </summary>
        public static void MigrateOldDefault(ConfigEntry<float> entry, float oldDefault, float newDefault)
        {
            if (entry == null || System.Math.Abs(entry.Value - oldDefault) > 0.0001f) return;
            entry.Value = newDefault;
            entry.ConfigFile.Save();
            MonsterLog.Info($"config: [{entry.Definition.Section}] {entry.Definition.Key} was the old default {oldDefault}; now the new default {newDefault}");
        }
        private static (float lo, float hi) Span(string key, float value)
        {
            string k = key.ToLowerInvariant();
            float big = System.Math.Max(10f, value * 10f);
            if (k.Contains("chance") || k.Contains("share") || k.Contains("fraction") || k.Contains("percent") || k.Contains("floor") || k.Contains("weight") && value <= 1f)
                return (0f, System.Math.Max(1f, value));
            if (k.Contains("second") || k.Contains("cooldown") || k.Contains("delay") || k.Contains("memory") || k.Contains("grace") || k.Contains("duration"))
                return (0f, System.Math.Max(3600f, value));
            if (k.Contains("radius") || k.Contains("range") || k.Contains("distance") && !k.Contains("bonus") || k.Contains("metre"))
                return (0f, System.Math.Max(500f, value));
            return (0f, big);
        }
    }
}
