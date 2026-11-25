using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;

namespace FiresMonsterOverhaul
{
    /// <summary>
    /// 0.0.47 ([ghost], Fire 10-30 "Core = our Jotunn", plan Tools\CORE_CONFIG_MOVE_PLAN_20261030.md): the Core settings FMO owns. Each is
    /// bound HERE under the same section + key and description Core used (Core 0.2.1043 Visuals/FitMeasure.cs), handed to Core with
    /// CoreSettings.Provide (Core's code reads it), and on the first load its value is copied once, read-only, from Core's cfg. Core
    /// switched "Measured helmet fit" on whenever FMO is installed (OwnerGatedConfig.AutoOnWith), so it defaults ON here.
    /// CoreSettings is reached by reflection (Core 0.2.1036+); with an older Core this logs once and Core keeps its own key.
    /// </summary>
    internal static class CoreSettingsOwned
    {
        private const string Owner = "FiresMonsterOverhaul";
        private static System.Reflection.MethodInfo s_provide, s_tryRead;

        internal static void Bind(ConfigFile config)
        {
            var t = AccessTools.TypeByName("FiresCore.Config.CoreSettings");
            s_provide = t != null ? AccessTools.Method(t, "Provide", new[] { typeof(string), typeof(string), typeof(ConfigEntryBase), typeof(string), typeof(int) }) : null;   // 0.0.49: the ConfigEntry overload (Core 0.2.1074+)
            s_tryRead = t != null ? AccessTools.Method(t, "TryReadCoreSaved") : null;
            if (s_provide == null) { MonsterLog.Info("[CoreSettings] the loaded Core has no ConfigEntry CoreSettings.Provide (older than 0.2.1074): Core keeps its own keys"); return; }
            // 0.0.49 ([generator], Fire 11-02): the other 3 keys and fall damage, Core's texts. Split's default follows Core's "on when FMO is installed".
            Own(config, "MonsterArmorFit", "Paint armour textures on monsters", false, "CLIENT. Off (default): vanilla. On: armour that vanilla paints onto the player's body (leather/rags legs, the leather chest's torso, the painted part of other armour) is painted onto a registered monster's own body texture.");
            Own(config, "MonsterArmorFit", "Vanilla bind on identical rigs", false, "CLIENT. On: a monster whose skeleton has the player's exact bone list (the Skeleton) keeps vanilla's own armour bind (exact there) instead of Core's refit. Off (default for now): Core refits it like every other monster.");
            Own(config, "MonsterArmorFit", "Split unmapped neck", true, "CLIENT. On: on a monster with no neck bone (the Draugr), an armour's neck vertices share their weight half with the head and half with the upper chest, so collars and coifs bend with the head. Off: they ride the upper chest only. Default: on when FiresMonsterOverhaul is installed.");
            s_fallDamage = Own(config, "Movement", "Monster fall damage", FiresCore.Movement.MonsterFallDamage.Mode.Off, "SERVER. Non-player characters (monsters, tames, NPCs) take the same fall damage players do: none up to 4 m, rising to 100 at 20 m. " + "Auto: off (no feature mod asks for it yet). On / Off: always.");
            Own(config, "MonsterArmorFit", "Measured helmet fit", true, "CLIENT. Rigid helmets on registered monsters are placed by MEASURING the monster's skull (from its own skin, in its pose) and " + "carrying the helmet over from where it sits on the player's measured skull: centre to centre, tilt to tilt, scaled by the skull-size " + "ratio. Off = vanilla's own helmet attach (plus the optional '<Model>|MonsterHelmet' nudge row). The console command " + "'fires_fit_debug on' draws the measured skull and helmet boxes. Default: on when FiresMonsterOverhaul is installed.");
        }

        private static ConfigEntry<FiresCore.Movement.MonsterFallDamage.Mode> s_fallDamage;

        internal static void BindToSync(FiresCore.Sync.ConfigSync sync)
        {
            if (s_fallDamage != null) sync.AddConfigEntry(s_fallDamage);
        }

        private static ConfigEntry<T> Own<T>(ConfigFile config, string section, string key, T def, string description)
        {
            var definition = new ConfigDefinition(section, key);
            bool hadOwn = HasSaved(config, definition);
            ConfigEntry<T> entry = config.Bind(definition, def, new ConfigDescription(description));
            if (!hadOwn && Equals(entry.BoxedValue, entry.DefaultValue) && TryReadCore(section, key, out string old))
            {
                try { entry.BoxedValue = TomlTypeConverter.ConvertToValue(old, typeof(T)); MonsterLog.Info($"[CoreSettings] moved {section}/{key} = {entry.GetSerializedValue()} from Core's cfg"); }
                catch (Exception ex) { MonsterLog.Warning($"[CoreSettings] {section}/{key}: Core's saved value '{old}' did not convert ({ex.Message}); default kept"); }
            }
            try { s_provide.Invoke(null, new object[] { section, key, entry, Owner, 0 }); }
            catch (Exception ex) { MonsterLog.Warning($"[CoreSettings] Provide {section}/{key} failed: {(ex.InnerException ?? ex).Message}"); }
            return entry;
        }

        private static bool TryReadCore(string section, string key, out string value)
        {
            value = null;
            if (s_tryRead == null) return false;
            var args = new object[] { section, key, null };
            try { bool ok = (bool)s_tryRead.Invoke(null, args); value = args[2] as string; return ok && value != null; }
            catch { return false; }
        }

        private static bool HasSaved(ConfigFile config, ConfigDefinition def)
        {
            try
            {
                var orphans = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config) as Dictionary<ConfigDefinition, string>;
                return orphans != null && orphans.ContainsKey(def);
            }
            catch { return false; }
        }
    }
}
