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
        public const string PluginVersion = "0.0.20";

        /// <summary>Hosts coroutines that outlive a creature (Death Burst's delayed explosion).</summary>
        internal static Plugin Instance { get; private set; }

        private Harmony _harmony;
        private ConfigSync _configSync;

        private void Awake()
        {
            Instance = this;
            MonsterConfig.Bind(Config);
            Setup.CreatureRules.LoadLocal();
            // The server's settings and creature rules apply on every peer: monsters are run by whichever peer owns them.
            _configSync = new ConfigSync(PluginGUID)
            {
                DisplayName = PluginName,
                CurrentVersion = PluginVersion,
                MinimumRequiredVersion = PluginVersion
            };
            MonsterConfig.BindToSync(_configSync);
            Setup.CreatureRules.BindToSync(_configSync);
            Roles.RoleRegistry.Load();
            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Threat.HealThreat.Subscribe();
            Packs.PackSystem.RegisterCommand();
            Tiers.MonsterTierTag.RegisterCommand();
            Casts.CastReadyCommand.Register();
            Setup.SetupCommands.Register();
            Bosses.BossCommands.Register();
            MonsterLog.Info($"{PluginName} {PluginVersion} loaded");
        }

        private void OnDestroy()
        {
            Threat.HealThreat.Unsubscribe();
        }
    }
}
