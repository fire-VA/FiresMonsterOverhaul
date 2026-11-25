using System.Reflection;
using BepInEx.Configuration;
using FiresCore.Sync;

namespace FiresMonsterOverhaul.Scaling
{
    /// <summary>
    /// Boss + mini-boss group scaling settings (Fire 10-12; 35-GROUP-SCALING-PLAN.md). Fire: "configurable, locked behind admin only
    /// and advanced config settings", "server-authoritative: the server's values are the only ones used". So they sit on their own
    /// ConfigSync whose lock is FORCED on (independent of [01 - General] Lock Configuration): every peer uses the server's values and
    /// only an admin can change them. Every entry is marked Advanced for the config manager. The defaults are Fire's answers.
    /// </summary>
    internal static class ScalingConfig
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> ReplaceVanilla;
        public static ConfigEntry<bool> MiniBossElites, MiniBossChampions, MiniBossHildir;
        public static ConfigEntry<float> EngageRadius, EngageMemory, RecountSeconds, ShrinkGrace;
        public static ConfigEntry<int> MaxPlayers;
        public static ConfigEntry<float> HpPerPlayer, ResistPerPlayer, DamagePerPlayer, ResistFloor;
        public static ConfigEntry<float> CompanionWeight;
        public static ConfigEntry<float> PowerStrength, PowerMin, PowerMax, PowerDamageShare;
        public static ConfigEntry<float> WeightGear, WeightQuality, WeightCrafting, WeightClasses, WeightSkills;
        public static ConfigEntry<string> ExpectedPerBiome;

        private static ConfigSync _sync;

        // Advanced flag now comes from the one settings-UI table (MonsterConfigUi, 10-20); no second ConfigurationManagerAttributes here.
        private static ConfigDescription D(string text) => new ConfigDescription(text + " ADMIN, server-authoritative.");
        private static ConfigDescription D(string text, AcceptableValueBase range) => new ConfigDescription(text + " ADMIN, server-authoritative.", range);

        public static void Bind(ConfigFile cfg)
        {
            const string s = "11 - Boss Scaling";
            Enabled = ConfigRanges.Bind(cfg, s, "Enabled", true, D("Bosses and mini bosses scale with the players ENGAGED in the fight: health and defence per extra player, damage at a lower rate, and with the group's power."));
            ReplaceVanilla = ConfigRanges.Bind(cfg, s, "Replace Vanilla Group Scaling", true, D("For bosses and mini bosses, this scaling REPLACES vanilla's own (+30 % effective health, +4 % damage per player within 100 m). Off: both apply."));
            MiniBossElites = ConfigRanges.Bind(cfg, s, "Mini Bosses Include Elites", true, D("Elite-tier monsters scale as mini bosses."));
            MiniBossChampions = ConfigRanges.Bind(cfg, s, "Mini Bosses Include Champions", true, D("Champion-tier (named) monsters scale as mini bosses."));
            MiniBossHildir = ConfigRanges.Bind(cfg, s, "Mini Bosses Include Hildir Bosses", true, D("Hildir's quest bosses (Brenna, Geirrhafa, Zil, Thungr) scale as mini bosses."));
            EngageRadius = ConfigRanges.Bind(cfg, s, "Engage Radius", 40f, D("Metres from the boss within which an engaged player or companion counts.", new AcceptableValueRange<float>(5f, 200f)));
            EngageMemory = ConfigRanges.Bind(cfg, s, "Engage Memory", 30f, D("A player or companion who hit the boss (or was its target) counts for this many seconds after."));
            RecountSeconds = ConfigRanges.Bind(cfg, s, "Recount Seconds", 2f, D("How often the boss's owner recounts the group during the fight.", new AcceptableValueRange<float>(0.5f, 30f)));
            ShrinkGrace = ConfigRanges.Bind(cfg, s, "Shrink Grace", 20f, D("Seconds the count holds its highest value, so a death or a step back doesn't shrink the boss."));
            MaxPlayers = ConfigRanges.Bind(cfg, s, "Max Players", 10, D("The most players the scaling counts.", new AcceptableValueRange<int>(1, 64)));
            HpPerPlayer = ConfigRanges.Bind(cfg, s, "HP Per Player", 0.6f, D("Extra health per extra engaged player (0.6 = +60 %)."));
            ResistPerPlayer = ConfigRanges.Bind(cfg, s, "Resist Per Player", 0.05f, D("Extra defence per extra engaged player (0.05 = it takes 1/1.05 the damage)."));
            DamagePerPlayer = ConfigRanges.Bind(cfg, s, "Damage Per Player", 0.15f, D("Extra damage per extra engaged player (0.15 = +15 %)."));
            ResistFloor = ConfigRanges.Bind(cfg, s, "Resist Floor", 0.35f, D("The least damage a scaled boss takes, as a share of a hit.", new AcceptableValueRange<float>(0.05f, 1f)));
            CompanionWeight = ConfigRanges.Bind(cfg, s, "Companion Weight", 0.5f, D("An engaged companion counts as this much of a player, with its owner's power (0 = companions don't count).", new AcceptableValueRange<float>(0f, 1f)));
            PowerStrength = ConfigRanges.Bind(cfg, s, "Power Strength", 0.5f, D("How strongly the group's power moves the boss: factor = 1 + strength x (group power - 1). 0 = off."));
            PowerMin = ConfigRanges.Bind(cfg, s, "Power Min", 0.8f, D("The lowest the power factor goes (an under-geared group)."));
            PowerMax = ConfigRanges.Bind(cfg, s, "Power Max", 6f, D("The highest the power factor goes (an over-geared group). 0.0.31: 2.5 -> 6, so bosses keep growing to meet fully built groups."));
            ConfigRanges.MigrateOldDefault(PowerMax, 2.5f, 6f);
            PowerDamageShare = ConfigRanges.Bind(cfg, s, "Power Damage Share", 0.5f, D("The share of the power factor that also raises the boss's damage (health takes all of it)."));
            WeightGear = ConfigRanges.Bind(cfg, s, "Power Weight Gear", 0.45f, D("Power score weight: worn armour + the best weapon's damage vs the biome's expected."));
            WeightQuality = ConfigRanges.Bind(cfg, s, "Power Weight Quality", 0.10f, D("Power score weight: the average upgrade level of the gear."));
            WeightCrafting = ConfigRanges.Bind(cfg, s, "Power Weight Crafting", 0.15f, D("Power score weight: Fires Loot Overhaul rarity, affixes and gems (expected when it isn't installed)."));
            WeightClasses = ConfigRanges.Bind(cfg, s, "Power Weight Classes", 0.15f, D("Power score weight: RPGClasses level (expected when RPGClasses isn't installed)."));
            WeightSkills = ConfigRanges.Bind(cfg, s, "Power Weight Skills", 0.15f, D("Power score weight: the equipped weapon's skill and Blocking vs the biome's expected."));
            ExpectedPerBiome = ConfigRanges.Bind(cfg, s, "Expected Power Per Biome",
                "Meadows=8:18:10,BlackForest=24:32:20,Swamp=40:48:30,Mountain=56:62:40,Plains=68:78:50,Mistlands=84:96:60,AshLands=100:120:70,DeepNorth=110:135:80",
                D("Per biome, the gear a group is expected to bring: armour:weapon damage:skill. A group at exactly this scores power 1.0."));
        }

        public static void BindToSync(string pluginGuid, string displayName, string version)
        {
            _sync = new ConfigSync(pluginGuid + ".bossscaling")
            {
                DisplayName = displayName + " (boss scaling)",
                CurrentVersion = version,
                MinimumRequiredVersion = Plugin.SyncMinVersion,
                IsLocked = true, // forced: only the server's values are used, only admins change them (Fire 10-12)
            };
            var add = typeof(ConfigSync).GetMethod(nameof(ConfigSync.AddConfigEntry));
            foreach (var field in typeof(ScalingConfig).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!(field.GetValue(null) is ConfigEntryBase) || !field.FieldType.IsGenericType) continue;
                add.MakeGenericMethod(field.FieldType.GetGenericArguments()[0]).Invoke(_sync, new[] { field.GetValue(null) });
            }
        }
    }
}
