using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using FiresCore.Sync;
using HarmonyLib;
using UnityEngine;

namespace FiresMonsterOverhaul
{
    // 0.0.47 ([ghost], Fire 10-30: "Creature diving goes into monster overhaul and is off unless movement is installed alongside it, so
    // if the player can dive, so can creatures"): moved here from FiresValheimGalaxies (Modules\Diving\CreatureDiving.cs, Galaxies
    // 0.1.65, unchanged logic). Runs only with FiresAdvancedPlayerMovement installed. Galaxies 0.1.66+ stands down its own copy when
    // CreatureDiving.Owned is true ([seasons]), so the prefix is patched once.
    /// <summary>
    /// Lets AI creatures DIVE underwater instead of being stuck floating on the
    /// surface. Vanilla holds a swimming Character at <c>liquidLevel -
    /// m_swimDepth</c> (Character.UpdateSwimming) — the body's vertical position
    /// is ENTIRELY a function of m_swimDepth; the AI only drives horizontal
    /// motion (UpdateSwimming zeroes the Y of its move force). So "diving" a
    /// creature is just: raise its m_swimDepth so it sinks to the depth of
    /// whatever it's pursuing.
    ///
    /// We drive m_swimDepth toward the creature's current GOAL:
    ///   * attack target (MonsterAI.m_targetCreature) — a predator follows a
    ///     diving player/prey under the surface, OR
    ///   * follow target (MonsterAI.GetFollowTarget) — a TAMED creature follows
    ///     its diving owner down.
    /// When the goal is at/above the surface (or there's no goal) the depth eases
    /// back to the creature's prefab default, so idle creatures stay on top.
    ///
    /// Owner-only (AI + physics run on the ZDO owner). Player Characters are
    /// skipped — player swimming is Core's (PlayerSwim). Wild creatures may drop an underwater
    /// target if MonsterAI can't path to it (vanilla behaviour); tamed-follow is
    /// the reliable case. AnimalAI (passive grazers) is not covered in v1 — they
    /// flee rather than pursue, so they have no underwater goal to dive toward.
    /// </summary>
    public static class CreatureDiving
    {
        // Master on/off (config). When off, the prefab default swim depth is
        // restored so creatures behave exactly like vanilla.
        public static bool  Enabled       = true;
        // How deep below the surface a creature will chase a goal (m). Caps how
        // far they'll sink so they don't bury themselves chasing a deep target.
        public static float MaxDiveDepth  = 30f;
        // How fast m_swimDepth changes (m/s) — the descend/ascend rate.
        public static float DescendSpeed  = 3f;

        /// <summary>FMO owns creature diving (true from FMO's Awake); Galaxies 0.1.66+ stands its old copy down on this.</summary>
        public static bool Owned { get; private set; }
        /// <summary>FiresAdvancedPlayerMovement installed (Fire: creatures dive only when players can).</summary>
        public static bool MovementInstalled { get; private set; }

        private const string Section = "Creature Diving", ApmGuid = "com.Fire.FiresAdvancedPlayerMovement";
        private const string GalaxiesCfg = "com.Fire.FiresValheimGalaxies.cfg", GalaxiesSection = "Galaxies - Diving";
        private static ConfigEntry<bool> s_enabled;
        private static ConfigEntry<float> s_maxDepth, s_speed;

        internal static void Bind(ConfigFile config)
        {
            Owned = true;
            s_enabled = Own(config, "CreatureDivingEnabled", true,
                new ConfigDescription("Let AI creatures dive to follow a diving target instead of being stuck at the surface. Only with Fires Advanced Player Movement installed."));
            s_maxDepth = Own(config, "CreatureMaxDiveDepth", 30f,
                new ConfigDescription("How far below the surface (m) a creature will sink chasing a goal.", new AcceptableValueRange<float>(2f, 100f)));
            s_speed = Own(config, "CreatureDiveSpeed", 3f,
                new ConfigDescription("How fast a creature descends/ascends (m/s) following its goal.", new AcceptableValueRange<float>(0.5f, 10f)));
            Apply();
            s_enabled.SettingChanged += (_, __) => Apply();
            s_maxDepth.SettingChanged += (_, __) => Apply();
            s_speed.SettingChanged += (_, __) => Apply();
        }

        internal static void BindToSync(ConfigSync sync)
        {
            sync.AddConfigEntry(s_enabled); sync.AddConfigEntry(s_maxDepth); sync.AddConfigEntry(s_speed);
        }

        /// <summary>After every plugin loaded (Chainloader complete): whether APM is there, and the one Info line saying so.</summary>
        internal static void CheckMovement()
        {
            MovementInstalled = Chainloader.PluginInfos.ContainsKey(ApmGuid);
            Apply();
            MonsterLogInfo(MovementInstalled
                ? $"[CreatureDiving] on (FMO + Fires Advanced Player Movement installed): {(Enabled ? "enabled" : "disabled in the config")}, max depth {MaxDiveDepth} m, speed {DescendSpeed} m/s"
                : "[CreatureDiving] off: Fires Advanced Player Movement is not installed (creatures dive only when players can)");
        }

        private static void Apply()
        {
            Enabled = MovementInstalled && (s_enabled == null || s_enabled.Value);
            if (s_maxDepth != null) MaxDiveDepth = s_maxDepth.Value;
            if (s_speed != null) DescendSpeed = s_speed.Value;
        }

        // the same key under FMO; on the first load without it, its value is copied once (read-only) from Galaxies' cfg
        private static ConfigEntry<T> Own<T>(ConfigFile config, string key, T def, ConfigDescription description)
        {
            var definition = new ConfigDefinition(Section, key);
            bool had = HasSaved(config, definition);
            var entry = config.Bind(definition, def, description);
            if (!had && TryReadGalaxies(key, out string old))
            {
                try
                {
                    entry.BoxedValue = TomlTypeConverter.ConvertToValue(old, typeof(T));
                    MonsterLogInfo($"[CreatureDiving] moved {key} = {entry.GetSerializedValue()} from Galaxies' cfg");
                }
                catch (System.Exception ex) { MonsterLogInfo($"[CreatureDiving] {key}: Galaxies' value '{old}' did not convert ({ex.Message}); default kept"); }
            }
            return entry;
        }

        private static bool TryReadGalaxies(string key, out string value)
        {
            value = null;
            try
            {
                string path = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, GalaxiesCfg);
                if (!System.IO.File.Exists(path)) return false;
                string section = null;
                foreach (var raw in System.IO.File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("[") && line.EndsWith("]")) { section = line.Substring(1, line.Length - 2); continue; }
                    if (section != GalaxiesSection || line.StartsWith("#") || line.Length == 0) continue;
                    int eq = line.IndexOf('=');
                    if (eq > 0 && line.Substring(0, eq).Trim() == key) { value = line.Substring(eq + 1).Trim(); return true; }
                }
            }
            catch { }
            return false;
        }

        private static bool HasSaved(ConfigFile config, ConfigDefinition def)
        {
            try
            {
                var orphans = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config) as System.Collections.Generic.Dictionary<ConfigDefinition, string>;
                return orphans != null && orphans.ContainsKey(def);
            }
            catch { return false; }
        }

        private static void MonsterLogInfo(string line) => MonsterLog.Info(line);
    }

    /// <summary>
    /// Prefix on Character.UpdateSwimming — runs immediately before vanilla reads
    /// m_swimDepth to compute the float height, so the value we set takes effect
    /// this same physics step.
    /// </summary>
    [HarmonyPatch(typeof(Character), "UpdateSwimming")]
    internal static class Character_UpdateSwimming_CreatureDive
    {
        // Per-creature prefab default swim depth, captured the first time we see
        // it (before we ever change m_swimDepth). ConditionalWeakTable auto-drops
        // the entry when the Character is GC'd, so no manual cleanup / leak.
        private static readonly ConditionalWeakTable<Character, StrongBox<float>> s_baseDepth
            = new ConditionalWeakTable<Character, StrongBox<float>>();

        private static FieldInfo s_targetField;

        private static void Prefix(Character __instance, float dt)
        {
            if (!CreatureDiving.Enabled || __instance == null || __instance.IsPlayer()) return;

            var ai = __instance.GetComponent<MonsterAI>();
            if (ai == null) return;

            var nview = __instance.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;

            // Capture the prefab's base swim depth ONCE, before we touch it.
            float baseDepth = s_baseDepth.GetValue(__instance, c => new StrongBox<float>(c.m_swimDepth)).Value;

            if (s_targetField == null)
                s_targetField = AccessTools.Field(typeof(MonsterAI), "m_targetCreature");

            // Current goal: attack target first, else follow target (tamed).
            Vector3? goal = null;
            if (s_targetField != null && s_targetField.GetValue(ai) is Character target && target != null)
                goal = target.transform.position;
            else
            {
                var follow = ai.GetFollowTarget();
                if (follow != null) goal = follow.transform.position;
            }

            float surface = __instance.GetLiquidLevel();
            float desired = baseDepth;
            // Only dive when the goal sits meaningfully BELOW the surface.
            if (goal.HasValue && goal.Value.y < surface - 0.5f)
                desired = Mathf.Clamp(surface - goal.Value.y, baseDepth, CreatureDiving.MaxDiveDepth);

            __instance.m_swimDepth = Mathf.MoveTowards(
                __instance.m_swimDepth, desired, CreatureDiving.DescendSpeed * dt);
        }
    }
}
