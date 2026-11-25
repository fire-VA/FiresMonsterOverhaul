using System.Globalization;
using System.Text;
using UnityEngine;

namespace FiresMonsterOverhaul.Setup
{
    /// <summary>
    /// Applies the creature rules once, on the creature's owner, a frame after it appears (before the tier roll, which
    /// reads the stars). Following [fgn]'s lessons from CLLC under Server-Side Simulation: position from the creature's
    /// spawn point, biome from WorldGenerator (works on a dedicated server and in dungeons), EnvMan for night, never the
    /// local player, and the owner applies it (FGN Selective gives the server the monsters near players).
    /// Health rides on vanilla's own recompute (GetMaxHealthBase x level) through the ZDO multiplier, so any later
    /// owner's SetLevel or Awake keeps it.
    /// </summary>
    internal static class CreatureSetup
    {
        public static readonly int ZdoSetup = "fmo_setup".GetStableHashCode();
        public static readonly int ZdoHealthMult = "fmo_hp_mult".GetStableHashCode();
        public static readonly int ZdoDamageMult = "fmo_dmg_mult".GetStableHashCode();
        private const float DungeonHeight = 4500f;
        private const int OneStarLevel = 2;
        private const int TwoStarLevel = 3;
        private const float FullHealthSlack = 0.01f;

        private static readonly string[] BossKeys =
        {
            "defeated_eikthyr", "defeated_gdking", "defeated_bonemass", "defeated_dragon",
            "defeated_goblinking", "defeated_queen", "defeated_fader",
        };

        public static Vector3 SpawnPoint(Character c)
        {
            var ai = c != null ? c.GetComponent<BaseAI>() : null;
            if (ai != null && ai.m_spawnPoint != Vector3.zero) return ai.m_spawnPoint;
            return c != null ? c.transform.position : Vector3.zero;
        }

        public static Heightmap.Biome BiomeAt(Vector3 pos) =>
            WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(pos) : Heightmap.Biome.None;

        // 0.0.46 (halo ring): a ring floor can sit above 4500 m; ring creatures are never "in a dungeon" (they kept dungeon tiers)
        public static bool InDungeon(Vector3 pos) => pos.y > DungeonHeight && !RingUp.OnRing(pos);

        private static System.Reflection.MethodInfo _privateTier;
        private static bool _privateLooked;

        /// <summary>
        /// The boss level at a point: the highest boss TIER (1 Eikthyr .. 7 Fader, 0 none) any player within [08 - Creature Setup]
        /// Boss Level Player Radius has defeated (Fire 10-13). Source: RPGmaker's private keys,
        /// FiresRPGmaker.Modules.Progression.PrivateKeyManager.GetHighestBossTierNear(Vector3, float) (contract, RPGmaker 0.0.572+;
        /// works on the dedi). -1 there (private keys off, nobody in range) or no RPGmaker: the highest global boss key.
        /// </summary>
        public static int BossLevel(Vector3 pos) => BossLevel(pos, out _);

        public static int BossLevel(Vector3 pos, out string source)
        {
            if (!_privateLooked)
            {
                _privateLooked = true;
                var t = System.Type.GetType("FiresRPGmaker.Modules.Progression.PrivateKeyManager, FiresRPGmaker");
                if (t == null)
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                        if ((t = asm.GetType("FiresRPGmaker.Modules.Progression.PrivateKeyManager", false)) != null) break;
                _privateTier = t?.GetMethod("GetHighestBossTierNear", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(Vector3), typeof(float) }, null);
                if (_privateTier != null && _privateTier.ReturnType != typeof(int)) _privateTier = null;
                MonsterLog.Debug(_privateTier != null ? "setup: boss level from RPGmaker PrivateKeyManager.GetHighestBossTierNear" : "setup: no RPGmaker GetHighestBossTierNear; boss level from global keys");
            }
            if (_privateTier != null)
            {
                try
                {
                    int tier = (int)_privateTier.Invoke(null, new object[] { pos, MonsterConfig.BossLevelRadius.Value });
                    if (tier >= 0) { source = "private keys"; return Mathf.Clamp(tier, 0, BossKeys.Length); }
                }
                catch (System.Exception e) { MonsterLog.Debug($"setup: GetHighestBossTierNear threw {e.GetType().Name}; global keys"); }
            }
            source = "global keys";
            return GlobalTier();
        }

        /// <summary>The highest boss tier in the world's global keys (1 Eikthyr .. 7 Fader, 0 none).</summary>
        private static int GlobalTier()
        {
            if (ZoneSystem.instance == null) return 0;
            for (int i = BossKeys.Length - 1; i >= 0; i--) if (ZoneSystem.instance.GetGlobalKey(BossKeys[i])) return i + 1;
            return 0;
        }

        /// <summary>Marks a creature as already set up (test spawns: fmo_tier, fmo_pack stay deterministic).</summary>
        public static void MarkDone(ZDO zdo)
        {
            if (zdo != null) zdo.Set(ZdoSetup, true);
        }

        /// <summary>Once per creature, on its owner. Neutral rules write nothing but the "done" flag.</summary>
        public static void Apply(Character c, ZNetView nview)
        {
            if (!MonsterConfig.CreatureSetupEnabled.Value || c == null || nview == null || !nview.IsValid() || !nview.IsOwner()) return;
            var zdo = nview.GetZDO();
            if (zdo.GetBool(ZdoSetup)) return;
            zdo.Set(ZdoSetup, true);
            if (!Tiers.MonsterTierTag.Eligible(c, nview)) return;

            Vector3 pos = SpawnPoint(c);
            var biome = BiomeAt(pos);
            bool dungeon = InDungeon(pos);
            bool night = EnvMan.IsNight();
            int bosses = BossLevel(pos, out string bossSource);
            string prefab = Utils.GetPrefabName(c.gameObject);
            var r = CreatureRules.Evaluate(prefab, biome, dungeon, night, bosses);
            if (r.Neutral) return;

            int before = c.GetLevel();
            int level = before;
            float roll = Random.value;
            if (roll < r.Star2) level = Mathf.Max(level, TwoStarLevel);
            else if (roll < r.Star2 + r.Star1) level = Mathf.Max(level, OneStarLevel);

            bool full = c.GetHealth() >= c.GetMaxHealth() - FullHealthSlack;
            if (r.Health != 1f) zdo.Set(ZdoHealthMult, r.Health);
            if (r.Damage != 1f) zdo.Set(ZdoDamageMult, r.Damage);
            c.SetLevel(level); // vanilla recomputes max health: GetMaxHealthBase (x our multiplier) x level
            if (full) c.SetHealth(c.GetMaxHealth());

            MonsterLog.Debug($"setup: {prefab} L{before}->L{level} hp x{r.Health:0.00} = {c.GetMaxHealth():0} dmg x{r.Damage:0.00} " +
                             $"({r.Why}; biome {biome}{(dungeon ? ", dungeon" : "")}{(night ? ", night" : "")}, bosslevel {bosses} ({bossSource}); " +
                             $"star roll {roll:0.000} vs 1* {r.Star1:0.000} / 2* {r.Star2:0.000}) {zdo.m_uid}");
        }

        /// <summary>The damage multiplier the setup gave this creature, times night's live factor.</summary>
        public static float DamageMultiplier(Character c)
        {
            if (c == null || !MonsterConfig.CreatureSetupEnabled.Value) return 1f;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return 1f;
            var zdo = nv.GetZDO();
            if (!zdo.GetBool(ZdoSetup)) return 1f;
            float m = zdo.GetFloat(ZdoDamageMult, 1f);
            if (m <= 0f) m = 1f;
            if (EnvMan.IsNight()) m *= CreatureRules.NightDamage();
            return m;
        }

        /// <summary>For fmo_setup_eval: the rules' answer for a prefab and situation, no spawn, no randomness.</summary>
        public static string Describe(string prefab, Heightmap.Biome biome, bool dungeon, bool night, int bosses)
        {
            var r = CreatureRules.Evaluate(prefab, biome, dungeon, night, bosses);
            var sb = new StringBuilder();
            sb.Append($"fmo_setup_eval: {prefab} in {biome}{(dungeon ? ", dungeon" : "")}{(night ? ", night" : "")}, bosslevel {bosses}: ");
            sb.Append(string.Format(CultureInfo.InvariantCulture, "hp x{0:0.00} dmg x{1:0.00} (night dmg x{2:0.00}) 1* {3:0.###} 2* {4:0.###}",
                r.Health, r.Damage, r.NightDamage, r.Star1, r.Star2));
            sb.Append($" [{r.Why}; {CreatureRules.ActiveCount} active rule(s)]");
            return sb.ToString();
        }
    }
}
