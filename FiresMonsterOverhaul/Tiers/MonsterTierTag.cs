using System.Collections;
using System.Collections.Generic;
using FiresMonsterOverhaul.Affixes;
using FiresMonsterOverhaul.Agent;
using UnityEngine;

namespace FiresMonsterOverhaul.Tiers
{
    /// <summary>
    /// The creature's tier and affixes (doc 21), decided once on its owner and kept on the ZDO, so every peer and every
    /// reload reads the same ones. Vanilla's spawners call SetLevel straight after Instantiate, so the star level is
    /// read a frame after Awake. Bosses, tames and possessed creatures get no tier.
    /// </summary>
    public sealed class MonsterTierTag : MonoBehaviour
    {
        public static readonly int ZdoTier = "fmo_tier".GetStableHashCode();
        public static readonly int ZdoAffixes = "fmo_affixes".GetStableHashCode();
        public static readonly int ZdoChampionName = "fmo_champ_name".GetStableHashCode();
        public static readonly int ZdoTierSet = "fmo_tier_set".GetStableHashCode();

        // Champion first names; the epithet comes from its first affix.
        private static readonly string[] ChampionNames =
        {
            "Grimbark", "Ulfgar", "Hrothnir", "Skarn", "Brokkr", "Vigdis", "Asmund", "Gunnhild",
            "Eldrid", "Haldor", "Ragna", "Thrain", "Sigrun", "Bjolf", "Hakon", "Ylva",
        };

        private ZNetView _nview;
        private Character _character;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _character = GetComponent<Character>();
        }

        private IEnumerator Start()
        {
            yield return null; // vanilla's spawner sets the star level right after Instantiate
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _character == null) yield break;
            Setup.CreatureSetup.Apply(_character, _nview); // the creature rules' stars come first: the tier roll reads them
            if (!MonsterConfig.TiersEnabled.Value) yield break;
            var zdo = _nview.GetZDO();
            if (zdo.GetBool(ZdoTierSet)) yield break;
            zdo.Set(ZdoTierSet, true);
            if (!Eligible(_character, _nview)) yield break;

            var tier = Roll(_character, out string why);
            var affixes = PickAffixes(_character, MonsterTierInfo.AffixCount(tier));
            Write(zdo, tier, affixes);
            if (tier != MonsterTier.Normal)
                MonsterLog.Debug($"tier: {Utils.GetPrefabName(gameObject)} -> {Describe(zdo)} ({why}) {_nview.GetZDO().m_uid}");
        }

        public static bool Eligible(Character c, ZNetView nview)
        {
            if (c == null || c.IsPlayer() || c.IsBoss() || c.IsTamed()) return false;
            return nview == null || !nview.IsValid() || nview.GetZDO().GetLong(MonsterBrain.ZdoPossessed) == 0L;
        }

        private static MonsterTier Roll(Character c, out string why)
        {
            int stars = Mathf.Max(0, c.GetLevel() - 1);
            // Spawn point + WorldGenerator: right on a dedicated server and inside dungeons ([fgn], 10-07).
            Vector3 p = Setup.CreatureSetup.SpawnPoint(c);
            var biome = Setup.CreatureSetup.BiomeAt(p);
            float distance = new Vector2(p.x, p.z).magnitude;
            float scale = 1f + MonsterConfig.TierDistanceBonus.Value * distance / 1000f;

            float champion = MonsterConfig.ChampionChance.Value * scale;
            float elite = champion + MonsterConfig.EliteChance.Value * scale;
            float veteran = elite + MonsterConfig.VeteranChance.Value * scale;
            float roll = Random.value;
            var tier = roll < champion ? MonsterTier.Champion : roll < elite ? MonsterTier.Elite : roll < veteran ? MonsterTier.Veteran : MonsterTier.Normal;

            if (MonsterConfig.StarsRaiseTier.Value)
            {
                var floor = stars >= 2 ? MonsterTier.Elite : stars == 1 ? MonsterTier.Veteran : MonsterTier.Normal;
                if (floor > tier) tier = floor;
            }
            if (biome == Heightmap.Biome.Meadows && !MonsterConfig.AffixesInMeadows.Value && tier > MonsterTier.Veteran)
                tier = MonsterTier.Veteran;

            why = $"stars {stars}, biome {biome}, d={distance:0} m, roll {roll:0.000} x{scale:0.00}";
            return tier;
        }

        /// <summary>
        /// Distinct enabled affixes this creature can answer (Stoneskin only on a creature that can be staggered),
        /// none of them in <paramref name="exclude"/>.
        /// </summary>
        internal static MonsterAffix PickAffixes(Character c, int count, MonsterAffix exclude = MonsterAffix.None)
        {
            var pool = new List<MonsterAffix>();
            foreach (var a in MonsterAffixInfo.All)
            {
                if (!MonsterAffixInfo.Enabled(a) || (exclude & a) != 0) continue;
                if (a == MonsterAffix.Stoneskin && c.m_staggerDamageFactor <= 0f) continue;
                pool.Add(a);
            }
            var mask = MonsterAffix.None;
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int pick = Random.Range(0, pool.Count);
                mask |= pool[pick];
                pool.RemoveAt(pick);
            }
            return mask;
        }

        // A Normal creature with no affixes keeps only the "decided" flag: an unset value reads as 0.
        private static void Write(ZDO zdo, MonsterTier tier, MonsterAffix affixes)
        {
            if (tier != MonsterTier.Normal || zdo.GetInt(ZdoTier, 0) != 0) zdo.Set(ZdoTier, (int)tier);
            if (affixes != MonsterAffix.None || zdo.GetInt(ZdoAffixes, 0) != 0) zdo.Set(ZdoAffixes, (int)affixes);
            if (tier != MonsterTier.Champion) return;
            string first = ChampionNames[Random.Range(0, ChampionNames.Length)];
            var lead = MonsterAffix.None;
            foreach (var a in MonsterAffixInfo.Each(affixes)) { lead = a; break; }
            zdo.Set(ZdoChampionName, $"{first} {MonsterAffixInfo.Epithet(lead)}");
        }

        public static MonsterTier TierOf(ZDO zdo) => zdo == null ? MonsterTier.Normal : (MonsterTier)zdo.GetInt(ZdoTier, 0);
        public static MonsterAffix AffixesOf(ZDO zdo) => zdo == null ? MonsterAffix.None : (MonsterAffix)zdo.GetInt(ZdoAffixes, 0);

        public static string Describe(ZDO zdo)
        {
            var tier = TierOf(zdo);
            string name = tier == MonsterTier.Champion ? " " + zdo.GetString(ZdoChampionName) : "";
            return $"{tier}{name} [{MonsterAffixInfo.Describe(AffixesOf(zdo))}]";
        }

        /// <summary>
        /// The test trigger (Fire 10-07: tests spawn the thing, on a controlled pad). "fmo_tier &lt;prefab&gt; &lt;tier&gt;
        /// [Affix,Affix]" spawns the creature 6 m ahead of the local player with that tier and those affixes (no roll),
        /// and prints its ZDOID so the test can clean up by ZDOID. With no affixes given, an Elite or Champion gets
        /// random ones, as a natural roll would.
        /// </summary>
        public static void RegisterCommand()
        {
            new Terminal.ConsoleCommand("fmo_tier", "[prefab] [Normal|Veteran|Elite|Champion] [Affix,Affix] - Fires Monster Overhaul: spawn a tiered monster 6 m ahead, for tests", args =>
            {
                if (args.Length < 3)
                {
                    args.Context?.AddString("usage: fmo_tier <prefab> <Normal|Veteran|Elite|Champion> [MendingAura,Stoneskin,Enrage,PackLeader,DeathBurst]");
                    return;
                }
                if (!System.Enum.TryParse(args[2], true, out MonsterTier tier)) { args.Context?.AddString($"fmo_tier: unknown tier '{args[2]}'"); return; }
                var affixes = MonsterAffix.None;
                if (args.Length >= 4)
                {
                    foreach (var part in args[3].Split(','))
                    {
                        if (!MonsterAffixInfo.TryParse(part, out var a)) { args.Context?.AddString($"fmo_tier: unknown affix '{part}'"); return; }
                        affixes |= a;
                    }
                }
                var player = Player.m_localPlayer;
                var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(args[1]) : null;
                if (player == null || prefab == null) { args.Context?.AddString("fmo_tier: no local player or unknown prefab"); return; }
                Vector3 p = player.transform.position + player.transform.forward * 6f;
                RingUp.Ground(ref p, 0.1f, "fmo spawn");   // 0.0.46: the ring floor on a halo ring, else the heightmap as before
                var go = Object.Instantiate(prefab, p, Quaternion.LookRotation(-player.transform.forward));
                var nview = go.GetComponent<ZNetView>();
                var character = go.GetComponent<Character>();
                if (nview == null || !nview.IsValid() || character == null || go.GetComponent<MonsterAI>() == null)
                {
                    args.Context?.AddString($"fmo_tier: {args[1]} is not a MonsterAI creature");
                    return;
                }
                if (affixes == MonsterAffix.None) affixes = PickAffixes(character, MonsterTierInfo.AffixCount(tier));
                var zdo = nview.GetZDO();
                zdo.Set(ZdoTierSet, true);
                Setup.CreatureSetup.MarkDone(zdo); // a test spawn keeps vanilla health, damage and stars
                Write(zdo, tier, affixes);
                string line = $"fmo_tier: created {args[1]}={zdo.m_uid} as {Describe(zdo)}";
                MonsterLog.Debug($"tier: {args[1]} -> {Describe(zdo)} (forced by fmo_tier) {zdo.m_uid}");
                args.Context?.AddString(line);
            }, isCheat: true);
        }
    }
}
