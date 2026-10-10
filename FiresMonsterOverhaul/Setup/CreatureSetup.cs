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

        public static bool InDungeon(Vector3 pos) => pos.y > DungeonHeight;

        /// <summary>Bosses defeated in this world (0-7), from the synced global keys.</summary>
        public static int BossLevel()
        {
            if (ZoneSystem.instance == null) return 0;
            int n = 0;
            foreach (var key in BossKeys) if (ZoneSystem.instance.GetGlobalKey(key)) n++;
            return n;
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
            int bosses = BossLevel();
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
                             $"({r.Why}; biome {biome}{(dungeon ? ", dungeon" : "")}{(night ? ", night" : "")}, bosslevel {bosses}; " +
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
