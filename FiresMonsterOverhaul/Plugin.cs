using BepInEx;
using FiresCore.Sync;
using HarmonyLib;

namespace FiresMonsterOverhaul
{
    // Fires Monster Overhaul: monsters that are dangerous because of what they do, every mechanic answered by the
    // class kit. Phase 1 (Tools\FiresMonsters\30-PHASE1-PLAN.md): MonsterAgent on Core's ICombatAgent seam, a threat
    // table, nameplate cast bars, stagger interrupts; Greydwarf Shaman heal + Draugr Elite war cry.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency("com.Fire.FiresUnifiedCore", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.Fire.FiresMonsterOverhaul";
        public const string PluginName = "Fires Monster Overhaul";
        public const string PluginVersion = "0.0.51";   // 0.0.51 ([ghost], Fire 11-05 log sweep): settings UI rules for the 3 Creature Diving keys (startup warning "no rule"). 0.0.50 was never deployed. 0.0.49 ([generator], Fire 11-02: configs live in their own mods): [Movement] Monster fall damage (synced) + the other 3 [MonsterArmorFit] keys bound here; all provided to Core as ConfigEntry. 0.0.48 ([ghost], Fire 10-31 event-driven / cache rule, same behaviour only): AffixRunner, MonsterBrain and BossDirector check their 0.5 s clock before reading the config switches (the reads ran every frame per monster; the work only ever ran after the clock). was 0.0.47:0.0.47 ([ghost], Fire 10-30): creature diving moved here from Galaxies (same code + 3 keys in [Creature Diving], values copied once from Galaxies' cfg), on only with Fires Advanced Player Movement installed; CreatureDiving.Owned tells Galaxies 0.1.66+ to stand down; owns Core's [MonsterArmorFit] "Measured helmet fit" (CoreSettings.Provide, value copied once from Core's cfg). 0.0.46 (Fire 10-24, halo ring: up re-targeted): a RingUp helper (Core GravityUp.InField by reflection; off a ring unchanged) - ring creatures are never "in a dungeon" (the +4500 m test), spawns / adds / boss props / phase spawns / packs / tier spawns snap to the ring floor and stand upright, the boss prop circle lies in the ring plane, sidesteps / dodges / dashes / blinks / roots face in the ring plane, the blast lane hit test + push run along the ring's up, gear drops lift along it. Proof "[RingUp] ... on a halo ring" (Debug). 0.0.44: fmo_boss_phase_test takes full-resolution shots (spawn, each phase, Elder entangle)
        /// <summary>The oldest version with the same synced key names, types and meanings (0.0.23 zero-padded the sections). Raise ONLY when a synced key changes.</summary>
        public const string SyncMinVersion = "0.0.23";

        /// <summary>Hosts coroutines that outlive a creature (Death Burst's delayed explosion).</summary>
        internal static Plugin Instance { get; private set; }

        private Harmony _harmony;
        private ConfigSync _configSync;

        private void Awake()
        {
            Instance = this;
            MonsterConfig.Bind(Config);
            Scaling.ScalingConfig.Bind(Config);
            CreatureDiving.Bind(Config);   // 0.0.47: creature diving (moved from Galaxies; on only with Fires Advanced Player Movement)
            CoreSettingsOwned.Bind(Config);   // 0.0.47: [MonsterArmorFit] Measured helmet fit, moved from Core
            ConfigRanges.MigratePaddedSections(Config);
            Setup.CreatureRules.LoadLocal();
            Gear.SetDrops.LoadLocal();
            // The server's settings and creature rules apply on every peer: monsters are run by whichever peer owns them.
            _configSync = new ConfigSync(PluginGUID)
            {
                DisplayName = PluginName,
                CurrentVersion = PluginVersion,
                MinimumRequiredVersion = SyncMinVersion
            };
            MonsterConfig.BindToSync(_configSync);
            CreatureDiving.BindToSync(_configSync);
            CoreSettingsOwned.BindToSync(_configSync);   // 0.0.49: [Movement] Monster fall damage (SERVER, as in Core)
            Setup.CreatureRules.BindToSync(_configSync);
            Gear.SetDrops.BindToSync(_configSync);
            // boss scaling: its own sync, lock forced on (server-authoritative, admin-only; Fire 10-12)
            Scaling.ScalingConfig.BindToSync(PluginGUID, PluginName, PluginVersion);
            // settings UI tags (names, descriptions, Advanced/Hidden), after every Bind and AddConfigEntry so the server lock is kept
            MonsterConfigUi.Apply(Config);
            Roles.RoleRegistry.Load();
            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Threat.HealThreat.Subscribe();
            Packs.PackSystem.RegisterCommand();
            Tiers.MonsterTierTag.RegisterCommand();
            Casts.CastReadyCommand.Register();
            Setup.SetupCommands.Register();
            Bosses.BossCommands.Register();
            Scaling.GroupScaler.RegisterCommand();
            Perf.FmoPerf.RegisterCommand();
            Gear.MonsterGear.RegisterCommand();
            Gear.MonsterGear.RegisterBodies();
            MonsterLog.Info($"{PluginName} {PluginVersion} loaded");
        }

        // every plugin has finished Awake by Start: whether Fires Advanced Player Movement is installed (creature diving's gate)
        private void Start() => CreatureDiving.CheckMovement();

        private void Update() => Scaling.PowerReporter.Tick();

        private void OnDestroy()
        {
            Threat.HealThreat.Unsubscribe();
        }
    }
}
