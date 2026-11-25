using System.Collections;
using System.Collections.Generic;
using FiresMonsterOverhaul.Roles;
using HarmonyLib;
using UnityEngine;

namespace FiresMonsterOverhaul.Packs
{
    /// <summary>
    /// Doc 21: "packs form around existing spawns — a spawn becomes a pack leader with role-based followers — so spawn
    /// configs keep working." Fire (10-07): Meadows + Black Forest first, on by default, 2–4 followers.
    /// The hook: SpawnSystem.Spawn marks what it is about to create; the creature's MonsterAI.Awake (inside that
    /// Instantiate) hands it to OnSpawned; the followers come a frame later, next to the leader, leashed to it.
    /// Runs where vanilla's spawner runs (the zone owner), so every follower is a normal networked creature.
    /// </summary>
    internal static class PackSystem
    {
        public const string ZdoPack = "fmo_pack";          // ZDOID of the pack's leader (string key: ZDOID value)
        public static readonly int ZdoLeader = "fmo_pack_leader".GetStableHashCode();

        // Leader prefab -> (biome it packs in, follower prefabs in priority order). The first N are used.
        private static readonly Dictionary<string, (Heightmap.Biome biome, string[] followers)> Compositions =
            new Dictionary<string, (Heightmap.Biome, string[])>
            {
                // Meadows
                { "Greyling", (Heightmap.Biome.Meadows, new[] { "Greyling", "Greyling", "Greyling", "Greyling" }) },
                { "Boar",     (Heightmap.Biome.Meadows, new[] { "Boar", "Boar", "Boar", "Boar" }) },
                { "Neck",     (Heightmap.Biome.Meadows, new[] { "Neck", "Neck", "Neck", "Neck" }) },
                // Black Forest (doc 21's example: interrupt the Shaman or the pack keeps healing)
                { "Greydwarf", (Heightmap.Biome.BlackForest, new[] { "Greydwarf_Shaman", "Greydwarf_Elite", "Greydwarf", "Greydwarf" }) },
            };

        private static SpawnSystem.SpawnData _pending;
        private static bool _pendingEvent;
        private static readonly List<Character> Scan = new List<Character>();
        private static float _lastPackTime;
        private static Vector3 _lastPackPos;

        public static void BeginSpawn(SpawnSystem.SpawnData critter, bool eventSpawner) { _pending = critter; _pendingEvent = eventSpawner; }
        public static void EndSpawn() { _pending = null; }

        /// <summary>Called from MonsterAI.Awake; picks up a creature vanilla's spawner is creating right now.</summary>
        public static void OnAwake(MonsterAI ai)
        {
            if (_pending == null || _pendingEvent || !MonsterConfig.PacksEnabled.Value) return;
            if (_pending.m_prefab == null || Utils.GetPrefabName(ai.gameObject) != _pending.m_prefab.name) return;
            string prefab = _pending.m_prefab.name;
            if (!Compositions.TryGetValue(prefab, out var comp)) return;
            Vector3 pos = ai.transform.position;
            if ((Setup.CreatureSetup.BiomeAt(pos) & comp.biome) == Heightmap.Biome.None) return;
            if (comp.biome == Heightmap.Biome.Meadows && !MonsterConfig.PacksMeadows.Value) return;
            if (comp.biome == Heightmap.Biome.BlackForest && !MonsterConfig.PacksBlackForest.Value) return;
            // Vanilla spawns a group with several Spawn calls in one go; only the first becomes a leader.
            if (Time.time - _lastPackTime < 3f && (pos - _lastPackPos).sqrMagnitude < 30f * 30f) return;
            if (!UnderBudget(pos)) return;
            _lastPackTime = Time.time;
            _lastPackPos = pos;
            ai.StartCoroutine(SpawnFollowers(ai, prefab, comp.followers, "natural spawn", null));
        }

        /// <summary>
        /// The test trigger (Fire 10-07: tests run only on a controlled pad, where natural spawning is suppressed).
        /// "fmo_pack &lt;leaderPrefab&gt;" spawns the leader 6 m in front of the local player and runs the SAME
        /// SpawnFollowers natural spawns use; biome and budget checks are skipped (the pad decides where).
        /// Prints every ZDOID it created so the test can clean up by ZDOID.
        /// </summary>
        public static void RegisterCommand()
        {
            new Terminal.ConsoleCommand("fmo_pack", "[leaderPrefab] - Fires Monster Overhaul: spawn a pack (leader + followers) 6 m ahead, for tests", args =>
            {
                if (args.Length < 2) { args.Context?.AddString("usage: fmo_pack <leaderPrefab>  (" + string.Join(", ", Compositions.Keys) + ")"); return; }
                string prefab = args[1];
                if (!Compositions.TryGetValue(prefab, out var comp)) { args.Context?.AddString($"fmo_pack: no pack composition for '{prefab}'"); return; }
                var player = Player.m_localPlayer;
                var leaderPrefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
                if (player == null || leaderPrefab == null) { args.Context?.AddString("fmo_pack: no local player or unknown prefab"); return; }
                Vector3 p = player.transform.position + player.transform.forward * 6f;
                RingUp.Ground(ref p, 0.1f, "fmo spawn");   // 0.0.46: the ring floor on a halo ring, else the heightmap as before
                var go = Object.Instantiate(leaderPrefab, p, Quaternion.identity);
                var ai = go.GetComponent<MonsterAI>();
                if (ai == null) { args.Context?.AddString($"fmo_pack: {prefab} has no MonsterAI"); return; }
                PinNormal(go.GetComponent<ZNetView>());
                var ctx = args.Context;
                ai.StartCoroutine(SpawnFollowers(ai, prefab, comp.followers, "fmo_pack command", ids =>
                    ctx?.AddString("fmo_pack: created " + string.Join(" ", ids)), pinNormal: true));
            }, isCheat: true);
        }

        private static bool UnderBudget(Vector3 pos)
        {
            var player = Player.GetClosestPlayer(pos, 200f);
            Vector3 center = player != null ? player.transform.position : pos;
            Scan.Clear();
            Character.GetCharactersInRange(center, 60f, Scan);
            int leaders = 0;
            foreach (var c in Scan)
            {
                var nv = c != null ? c.GetComponent<ZNetView>() : null;
                if (nv != null && nv.IsValid() && nv.GetZDO().GetBool(ZdoLeader)) leaders++;
            }
            return leaders < MonsterConfig.MaxPacksNearPlayer.Value;
        }

        // A test command's pack is untiered and keeps vanilla stats (no tier roll, no creature setup), so a drill's
        // subjects behave the same every run.
        private static void PinNormal(ZNetView nv)
        {
            if (nv == null || !nv.IsValid()) return;
            nv.GetZDO().Set(Tiers.MonsterTierTag.ZdoTierSet, true);
            Setup.CreatureSetup.MarkDone(nv.GetZDO());
        }

        private static IEnumerator SpawnFollowers(MonsterAI leaderAi, string leaderPrefab, string[] followers, string trigger, System.Action<List<string>> created, bool pinNormal = false)
        {
            yield return null; // out of vanilla's Instantiate; the leader's ZDO is set up
            if (leaderAi == null) yield break;
            var leaderNv = leaderAi.GetComponent<ZNetView>();
            var leader = leaderAi.GetComponent<Character>();
            if (leaderNv == null || !leaderNv.IsValid() || !leaderNv.IsOwner() || leader == null) yield break;

            int min = Mathf.Clamp(MonsterConfig.PackFollowersMin.Value, 0, followers.Length);
            int max = Mathf.Clamp(MonsterConfig.PackFollowersMax.Value, min, followers.Length);
            int count = Random.Range(min, max + 1);
            ZDOID leaderId = leaderNv.GetZDO().m_uid;
            var ids = new List<string> { $"{leaderPrefab}={leaderId}" };
            if (count <= 0) { created?.Invoke(ids); yield break; }

            leaderNv.GetZDO().Set(ZdoLeader, true);
            leaderNv.GetZDO().Set(ZdoPack, leaderId);
            var made = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(followers[i]) : null;
                if (prefab == null) continue;
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(2.5f, 5f);
                Vector3 p = leader.transform.position + new Vector3(off.x, 0f, off.y);
                RingUp.Ground(ref p, 0.1f, "fmo spawn");   // 0.0.46: the ring floor on a halo ring, else the heightmap as before
                var go = Object.Instantiate(prefab, p, RingUp.Upright(p, Random.Range(0f, 360f)));   // 0.0.46: upright to a halo ring
                var nv = go.GetComponent<ZNetView>();
                if (nv != null && nv.IsValid()) { nv.GetZDO().Set(ZdoPack, leaderId); ids.Add($"{followers[i]}={nv.GetZDO().m_uid}"); }
                if (pinNormal) PinNormal(nv);
                var ai = go.GetComponent<MonsterAI>();
                if (ai != null)
                {
                    ai.SetFollowTarget(leader.gameObject);
                    if (leaderAi.HuntPlayer()) ai.SetHuntPlayer(true);
                    if (leaderAi.DespawnInDay()) ai.SetDespawnInDay(true);
                }
                made.Add(followers[i]);
            }
            MonsterLog.Debug($"pack: {leaderPrefab} leader + {made.Count} follower(s) ({string.Join(", ", made)}) at {leader.transform.position:F0} ({Setup.CreatureSetup.BiomeAt(leader.transform.position)}) via {trigger}; ZDOIDs {string.Join(" ", ids)}");
            created?.Invoke(ids);
        }
    }

    [HarmonyPatch(typeof(SpawnSystem), nameof(SpawnSystem.Spawn))]
    internal static class SpawnPackPatch
    {
        private static void Prefix(SpawnSystem.SpawnData critter, bool eventSpawner) => PackSystem.BeginSpawn(critter, eventSpawner);
        private static void Finalizer() => PackSystem.EndSpawn();
    }
}
