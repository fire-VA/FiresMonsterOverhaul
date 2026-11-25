using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using FiresCore.Sync;
using HarmonyLib;
using UnityEngine;

namespace FiresMonsterOverhaul.Gear
{
    /// <summary>
    /// Set drops (Tools\FMO_SET_DROPS_PLAN.md, Fire 10-14; [13 - Set Drops] Enabled, default ON since 0.0.41). Fires Loot Overhaul set
    /// pieces drop only from a set's SOURCE, ESO-style, per FiresMonsterOverhaul.set_drops.yaml (server-synced):
    ///   miniboss     - a named miniboss: its prefab, as a Champion (Hildir's quest bosses at any tier);
    ///   dungeon_boss - a creature FDM spawned inside that dungeon (fdm_dungeon / DungeonApi.DungeonOf), from the
    ///                  dungeon's boss prefab list (an empty list = any Champion in it);
    ///   area_mobs    - any non-boss, non-tame mob killed in the biome;
    ///   champion     - a random FMO Champion in the biome.
    /// PERSONAL loot: the killer's owner sends each engaged player one roll request; that player's own client rolls with
    /// their own bad-luck counter (custom player data fmo_setluck_&lt;setId&gt;, it follows the character) and drops the piece
    /// at the kill. The slot is one the player doesn't own yet for that set (inventory and equipped), else any.
    /// A set piece is only ever written by FloApi.Roll(..., setId), so crafted items are never set pieces.
    /// Stage 1: fixed miniboss spawn points with a respawn timer come in stage 2.
    /// </summary>
    internal static class SetDrops
    {
        private const string FileName = "FiresMonsterOverhaul.set_drops.yaml";
        private const string SyncId = "fmo_set_drops";
        private const string RollRpc = "FMO_SetRoll";
        private const string LuckKey = "fmo_setluck_";

        internal sealed class SetRow
        {
            public string id { get; set; } = "";
            public string source { get; set; } = "";
            public List<string> targets { get; set; } = new List<string>();
            public float chance { get; set; }
            public List<string> slots { get; set; } = new List<string>();
            /// <summary>Bad-luck step per miss for this set; below 0 = chance x [13 - Set Drops] Bad Luck Share.</summary>
            public float bad_luck_step { get; set; } = -1f;
        }

        internal sealed class MinibossRow
        {
            public string id { get; set; } = "";
            public string prefab { get; set; } = "";
            public bool any_tier { get; set; }
            public int respawn_minutes { get; set; } = 60;
            /// <summary>0.0.36: the biome it matches in; "" = the biome in its set id (flo_set_<biome>_*), "any" = everywhere.</summary>
            public string biome { get; set; } = "";
        }

        internal sealed class DungeonRow
        {
            public string id { get; set; } = "";
            public List<string> bosses { get; set; } = new List<string>();
        }

        internal sealed class Data
        {
            public int version { get; set; } = 1;
            public List<SetRow> sets { get; set; } = new List<SetRow>();
            public List<MinibossRow> minibosses { get; set; } = new List<MinibossRow>();
            public List<DungeonRow> dungeons { get; set; } = new List<DungeonRow>();
        }

        private static Data _data = new Data();
        private static CustomSyncedValue<string> _synced;
        private static string _localText = "";
        private static ZRoutedRpc _registeredOn;

        public static string FilePath => Path.Combine(Paths.ConfigPath, FileName);

        // ── data ─────────────────────────────────────────────────────────────────────────────────────────────
        public static void LoadLocal()
        {
            try
            {
                if (!File.Exists(FilePath)) File.WriteAllText(FilePath, Template, new UTF8Encoding(false));
                _localText = File.ReadAllText(FilePath);
            }
            catch (Exception ex)
            {
                MonsterLog.Warning($"set drops: could not read {FileName}: {ex.Message}");
                _localText = "";
            }
            if (_synced != null) _synced.AssignLocalValue(_localText);
            else Parse(_localText, "local file");
        }

        public static void BindToSync(ConfigSync sync)
        {
            _synced = new CustomSyncedValue<string>(sync, SyncId, _localText);
            _synced.ValueChanged += () => Parse(_synced.Value ?? "", "synced");
            Parse(_synced.Value ?? "", "synced");
        }

        private static void Parse(string text, string source)
        {
            Data d;
            try { d = Yaml.Read(text); }
            catch (Exception ex)
            {
                // YamlDotNet missing (a manifest dependency) or a bad file: keep the last good data
                MonsterLog.Info($"set drops ({source}): {FileName} not read ({ex.GetType().Name}: {ex.Message}); set drops unchanged");
                return;
            }
            if (d == null) { MonsterLog.Info($"set drops ({source}): {FileName} is empty; no set drops"); _data = new Data(); return; }
            var known = new HashSet<string>(Flo.SetIds(), StringComparer.OrdinalIgnoreCase);
            var keep = new List<SetRow>();
            foreach (var s in d.sets ?? new List<SetRow>())
            {
                if (s == null || string.IsNullOrEmpty(s.id)) continue;
                string src = (s.source ?? "").Trim().ToLowerInvariant();
                if (src != "miniboss" && src != "dungeon_boss" && src != "area_mobs" && src != "champion")
                { MonsterLog.Info($"set_drops: set {s.id}: unknown source '{s.source}', skipped"); continue; }
                if (known.Count > 0 && !known.Contains(s.id))
                { MonsterLog.Info($"set_drops: set {s.id}: not a FLO set id, skipped"); continue; }
                s.source = src;
                s.targets = s.targets ?? new List<string>();
                s.slots = s.slots ?? new List<string>();
                keep.Add(s);
            }
            d.sets = keep;
            d.minibosses = d.minibosses ?? new List<MinibossRow>();
            d.dungeons = d.dungeons ?? new List<DungeonRow>();
            _data = d;
            MonsterLog.Debug($"set drops ({source}): {keep.Count} set(s), {d.minibosses.Count} miniboss(es), {d.dungeons.Count} dungeon(s); FLO knows {known.Count} set id(s)");
        }

        // ── kill (the dying creature's owner) ───────────────────────────────────────────────────────────────
        internal static void OnKill(Character c)
        {
            if (!MonsterConfig.SetDropsEnabled.Value || c == null || c.IsPlayer() || c.IsTamed() || _data.sets.Count == 0) return;
            var nview = c.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
            if (!Flo.SetsAvailable) { MonsterLog.Debug("set drops: FLO has no set API (older than 0.1.2): nothing drops"); return; }
            string prefab = Utils.GetPrefabName(c.gameObject);
            if (nview.GetZDO().GetBool(Affixes.AffixRunner.ZdoNoDrops))
            { MonsterLog.Debug($"set drops: {prefab} is an FMO add/twin (fmo_nodrops), no roll {nview.GetZDO().m_uid}"); return; }
            var biome = Setup.CreatureSetup.BiomeAt(c.transform.position);
            var tier = Tiers.MonsterTierTag.TierOf(nview.GetZDO());
            string dungeon = Fdm.DungeonOf(c);
            var hits = _data.sets.Where(s => Matches(s, c, prefab, biome, tier, dungeon, out _)).ToList();
            if (hits.Count == 0) return;
            // 0.0.36 (R3: a Champion Boar matched miniboss + champion + area_mobs = 3 rolls): one roll per kill per player, from the
            // most specific source; ties keep the YAML order
            if (hits.Count > 1)
            {
                var best = hits.OrderBy(s => Rank(s.source)).First();
                MonsterLog.Debug($"set drops: {prefab} matched {hits.Count} sets; rolls {best.id} ({best.source}), skips {string.Join(", ", hits.Where(h => h != best).Select(h => $"{h.id} ({h.source})"))}");
                hits = new List<SetRow> { best };
            }
            var players = Engaged(c);
            if (players.Count == 0) { MonsterLog.Debug($"set drops: {prefab} died with no engaged player"); return; }
            EnsureRegistered();
            foreach (var s in hits)
            {
                Matches(s, c, prefab, biome, tier, dungeon, out string why);
                foreach (var p in players)
                {
                    long peer = p.GetComponent<ZNetView>()?.GetZDO()?.GetOwner() ?? 0L;
                    if (peer == 0L) continue;
                    var pkg = new ZPackage(); pkg.Write(p.GetZDOID()); pkg.Write(s.id); pkg.Write(s.chance); pkg.Write((int)biome); pkg.Write(c.transform.position + Vector3.up * 0.8f); pkg.Write(why); pkg.Write(string.Join(",", s.slots)); pkg.Write(StepOf(s));
                    ZRoutedRpc.instance.InvokeRoutedRPC(peer, RollRpc, pkg);
                }
                MonsterLog.Debug($"set drops: {prefab} ({why}) -> {s.id} roll for {players.Count} player(s) at {s.chance:0.###} each");
            }
        }

        /// <summary>The per-miss bad-luck step: the YAML override, else the set's chance x Bad Luck Share (Fire 10-14: +8% flat swamped 0.5% sets).</summary>
        private static float StepOf(SetRow s) => s.bad_luck_step >= 0f ? s.bad_luck_step : Mathf.Max(0f, s.chance) * Mathf.Max(0f, MonsterConfig.SetDropsBadLuckShare.Value);

        private static int Rank(string source) => source == "miniboss" ? 0 : source == "dungeon_boss" ? 1 : source == "champion" ? 2 : 3;

        /// <summary>A miniboss row's biome: its own field, else the biome in the set id (flo_set_meadows_heavy -> Meadows).</summary>
        private static bool MinibossInBiome(MinibossRow m, SetRow s, Heightmap.Biome biome)
        {
            string b = (m.biome ?? "").Trim();
            if (b.Equals("any", StringComparison.OrdinalIgnoreCase)) return true;
            if (b.Length == 0)
            {
                var parts = (s.id ?? "").Split('_');
                b = parts.Length >= 4 ? parts[2] : "";
            }
            if (b.Length == 0) return true;
            return Enum.TryParse(b, true, out Heightmap.Biome want) && (biome & want) != 0;
        }

        private static bool Matches(SetRow s, Character c, string prefab, Heightmap.Biome biome, Tiers.MonsterTier tier, string dungeon, out string why)
        {
            why = s.source;
            bool champion = tier == Tiers.MonsterTier.Champion;
            switch (s.source)
            {
                case "champion":
                    if (!champion || !InBiomes(s.targets, biome)) return false;
                    why = $"champion in {biome}"; return true;
                case "area_mobs":
                    if (c.IsBoss() || !InBiomes(s.targets, biome)) return false;
                    why = $"area mob in {biome}"; return true;
                case "miniboss":
                    foreach (var t in s.targets)
                    {
                        var m = _data.minibosses.FirstOrDefault(x => string.Equals(x.id, t, StringComparison.OrdinalIgnoreCase));
                        if (m == null || !string.Equals(m.prefab, prefab, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!m.any_tier && !champion) continue;
                        if (!MinibossInBiome(m, s, biome)) continue;
                        why = $"miniboss {m.id}"; return true;
                    }
                    return false;
                case "dungeon_boss":
                    if (string.IsNullOrEmpty(dungeon)) return false;
                    foreach (var t in s.targets)
                    {
                        if (!string.Equals(t, dungeon, StringComparison.OrdinalIgnoreCase)) continue;
                        var dr = _data.dungeons.FirstOrDefault(x => string.Equals(x.id, dungeon, StringComparison.OrdinalIgnoreCase));
                        bool boss = dr != null && dr.bosses.Count > 0 ? dr.bosses.Any(b => string.Equals(b, prefab, StringComparison.OrdinalIgnoreCase)) : champion;
                        if (!boss) continue;
                        why = $"dungeon boss of {dungeon}"; return true;
                    }
                    return false;
            }
            return false;
        }

        private static bool InBiomes(List<string> targets, Heightmap.Biome biome)
        {
            foreach (var t in targets)
                if (Enum.TryParse(t.Trim(), true, out Heightmap.Biome b) && (biome & b) != 0) return true;
            return false;
        }

        /// <summary>Players on the creature's threat table within Engage Range, or within a third of it (threat can be
        /// cleared on death).</summary>
        private static List<Player> Engaged(Character c)
        {
            float range = Mathf.Max(1f, MonsterConfig.SetDropsEngageRange.Value);
            var threat = c.GetComponent<Threat.ThreatTable>();
            var list = new List<Player>();
            foreach (var p in Player.GetAllPlayers())
            {
                if (p == null || p.IsDead()) continue;
                float d = Vector3.Distance(p.transform.position, c.transform.position);
                if (d > range) continue;
                if (d <= range / 3f || (threat != null && threat.Has(p.GetZDOID()))) list.Add(p);
            }
            return list;
        }

        // ── the roll (each engaged player's own client) ────────────────────────────────────────────────────────
        internal static void EnsureRegistered()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc == _registeredOn) return;
            rpc.Register<ZPackage>(RollRpc, OnRollPkg);
            _registeredOn = rpc;
        }

        private static void OnRollPkg(long sender, ZPackage pkg)
        {
            try
            {
                ZDOID id = pkg.ReadZDOID(); string set = pkg.ReadString(); float chance = pkg.ReadSingle(); int biome = pkg.ReadInt(); Vector3 pos = pkg.ReadVector3(); string why = pkg.ReadString(); string slots = pkg.ReadString();
                float step = pkg.GetPos() < pkg.Size() ? pkg.ReadSingle() : chance * 0.25f;   // 0.0.35: the sender's step
                OnRoll(id, set, chance, biome, pos, why, slots, step);
            }
            catch (Exception e) { MonsterLog.Debug($"set roll: bad request ({e.GetType().Name})"); }
        }

        private static void OnRoll(ZDOID playerId, string setId, float chance, int biome, Vector3 pos, string why, string slots, float step)
        {
            var me = Player.m_localPlayer;
            if (me == null || me.GetZDOID() != playerId || !MonsterConfig.SetDropsEnabled.Value) return;
            string key = LuckKey + setId;
            float luck = 0f;
            if (me.m_customData.TryGetValue(key, out string ls)) float.TryParse(ls, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out luck);
            float p = Mathf.Clamp01(chance + luck), roll = UnityEngine.Random.value;
            if (roll >= p)
            {
                float next = luck + Mathf.Max(0f, step);
                me.m_customData[key] = next.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                MonsterLog.Debug($"set roll: {why} -> {setId} base {chance:0.###} +luck {luck:0.###} = {p:0.###}, rolled {roll:0.###}, no drop; luck +{step:0.#####} -> {next:0.#####}");
                return;
            }
            var item = MakePiece(me, setId, slots, out string slot);
            if (item == null) { MonsterLog.Debug($"set roll: {setId} won but no piece could be made (slots {slots})"); return; }
            int rarity = UnityEngine.Random.value < MonsterConfig.SetDropsLegendaryChance.Value ? 4 : 3;
            if (!Flo.Roll(item, rarity, 3, (Heightmap.Biome)biome, 1f, setId))
            { MonsterLog.Debug($"set roll: FloApi.Roll refused {item.m_dropPrefab?.name} for {setId} (FLO [Sets] off or unknown id)"); return; }
            ItemDrop.DropItem(item, 1, pos, Quaternion.identity);
            me.m_customData.Remove(key);
            MonsterLog.Info($"set drop: {why} -> {setId} {slot} {item.m_dropPrefab?.name} ({(rarity == 4 ? "Legendary" : "Epic")}) for {me.GetPlayerName()}; luck +{luck:0.###} reset");
        }

        /// <summary>A plain item for a slot the player doesn't own yet in this set (else any listed slot).</summary>
        private static ItemDrop.ItemData MakePiece(Player me, string setId, string slots, out string slot)
        {
            slot = null;
            var all = (slots ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            bool fromFlo = all.Count == 0;
            if (fromFlo) all = Flo.SetSlots().ToList();   // 0.0.34: an empty slots list = FLO's active slots
            MonsterLog.Debug($"set slots: {setId} from {(fromFlo ? "FLO SetSlots()" : "set_drops.yaml")}: {string.Join(",", all)}");
            if (all.Count == 0) return null;
            var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in me.GetInventory()?.GetAllItems() ?? new List<ItemDrop.ItemData>())
            {
                if (!string.Equals(Flo.GetSet(it), setId, StringComparison.OrdinalIgnoreCase)) continue;
                string name = it.m_dropPrefab != null ? it.m_dropPrefab.name : "";
                foreach (var s in all) if (Flo.SetPieces(setId, s).Contains(name, StringComparer.OrdinalIgnoreCase)) owned.Add(s);
            }
            var open = all.Where(s => !owned.Contains(s)).ToList();
            if (open.Count == 0) open = all;
            foreach (var s in open.OrderBy(_ => UnityEngine.Random.value))
            {
                var pieces = Flo.SetPieces(setId, s);
                if (pieces.Length == 0) continue;
                string prefab = pieces[UnityEngine.Random.Range(0, pieces.Length)];
                var go = ObjectDB.instance?.GetItemPrefab(prefab);
                var drop = go != null ? go.GetComponent<ItemDrop>() : null;
                if (drop == null) continue;
                var item = drop.m_itemData.Clone();
                item.m_dropPrefab = go;
                item.m_quality = 1;
                item.m_durability = item.GetMaxDurability();
                item.m_stack = 1;
                slot = s;
                return item;
            }
            return null;
        }

        // ── defaults: FLO's 21 base sets, the mapping Fire approved 10-14 01:20 ───────────────────────────────────
        private const string Template = @"# Fires Monster Overhaul: set drops ([13 - Set Drops] Enabled, default ON with Fires Loot Overhaul). The server's copy is synced to everyone.
# Each FLO set has ONE source (dungeon_boss needs FiresDungeonMaster 0.1.168+; dungeon boss picks from [seasons] 10-14):
#   miniboss     - targets = ids from 'minibosses' below (a Champion of that prefab; any_tier: true for Hildir's bosses)
#   dungeon_boss - targets = FDM dungeon ids; the boss = that dungeon's 'bosses' prefab list below (empty = any Champion in it)
#   area_mobs    - targets = biome names; any non-boss mob killed there
#   champion     - targets = biome names; a random FMO Champion there
# chance = per engaged player, per kill (personal loot), plus that player's bad-luck counter.
# bad_luck_step = optional per set: added per miss (default: chance x [13 - Set Drops] Bad Luck Share). FMO adds/twins never roll.
# slots  = FLO set slots: Helmet, Chest, Legs, Cape, Ring (FLO 0.1.5 ESO slots: Ring1, Ring2, Necklace, MainHand, OffHand); [] = FLO's active list.
version: 1
sets:
  - { id: flo_set_meadows_heavy,       source: miniboss,     targets: [tusk_mother],        chance: 0.35,  slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_meadows_medium,      source: area_mobs,    targets: [Meadows],            chance: 0.005, slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_meadows_light,       source: champion,     targets: [Meadows],            chance: 0.03,  slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_blackforest_heavy,   source: miniboss,     targets: [brenna],             chance: 0.35,  slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_blackforest_medium,  source: area_mobs,    targets: [BlackForest],        chance: 0.005, slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_blackforest_light,   source: dungeon_boss, targets: [burial_chambers],   chance: 0.25,  slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_blackforest_light,   source: dungeon_boss, targets: [troll_cave],         chance: 0.10,  slots: [Helmet, Chest, Legs, Cape] }   # Fire 10-14: Trolls are common in troll caves
  - { id: flo_set_swamp_heavy,         source: miniboss,     targets: [abomination],        chance: 0.35,  slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_swamp_medium,        source: area_mobs,    targets: [Swamp],              chance: 0.005, slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_swamp_light,         source: dungeon_boss, targets: [sunken_crypt],       chance: 0.25,  slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_mountain_heavy,      source: miniboss,     targets: [geirrhafa],          chance: 0.35,  slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_mountain_medium,     source: area_mobs,    targets: [Mountain],           chance: 0.005, slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_mountain_light,      source: dungeon_boss, targets: [frost_cave],         chance: 0.25,  slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_plains_heavy,        source: miniboss,     targets: [zil, thungr],        chance: 0.35,  slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_plains_medium,       source: area_mobs,    targets: [Plains],             chance: 0.005, slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_plains_light,        source: champion,     targets: [Plains],             chance: 0.03,  slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_mistlands_heavy,     source: miniboss,     targets: [seeker_soldier],     chance: 0.35,  slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_mistlands_medium,    source: area_mobs,    targets: [Mistlands],          chance: 0.005, slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_mistlands_light,     source: dungeon_boss, targets: [infested_mine],      chance: 0.25,  slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_ashlands_heavy,      source: miniboss,     targets: [fallen_valkyrie],    chance: 0.35,  slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_ashlands_medium,     source: area_mobs,    targets: [AshLands],           chance: 0.005, slots: [Helmet, Chest, Legs, Cape] }
  - { id: flo_set_ashlands_light,      source: dungeon_boss, targets: [charred_fortress],   chance: 0.25,  slots: [Helmet, Chest, Legs, Cape] }
minibosses:
  - { id: tusk_mother,     prefab: Boar }
  - { id: brenna,          prefab: Skeleton_Hildir,        any_tier: true }
  - { id: abomination,     prefab: Abomination }
  - { id: geirrhafa,       prefab: Fenring_Cultist_Hildir, any_tier: true }
  - { id: zil,             prefab: GoblinBruteBros,        any_tier: true }
  - { id: thungr,          prefab: GoblinShaman_Hildir,    any_tier: true }
  - { id: seeker_soldier,  prefab: SeekerBrute }
  - { id: fallen_valkyrie, prefab: FallenValkyrie }
dungeons:
  - { id: burial_chambers,  bosses: [Skeleton_Poison] }
  - { id: troll_cave,       bosses: [Troll] }
  - { id: sunken_crypt,     bosses: [Draugr_Elite] }
  - { id: frost_cave,       bosses: [Fenring_Cultist, Ulv] }
  - { id: infested_mine,    bosses: [SeekerBrute] }
  - { id: charred_fortress, bosses: [Charred_Mage, Charred_Melee_Dyrnwyn] }
";
    }

    /// <summary>YamlDotNet (the shared ValheimModding-YamlDotNet package, a manifest dependency, never bundled), kept in its
    /// own class so a missing package fails here, inside SetDrops.Parse's try, and nowhere else.</summary>
    internal static class Yaml
    {
        public static SetDrops.Data Read(string text)
        {
            var de = new YamlDotNet.Serialization.DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            return de.Deserialize<SetDrops.Data>(text ?? "");
        }
    }

    /// <summary>FiresDungeonMaster's DungeonApi.DungeonOf(Character) by reflection (FDM 0.1.167+, owned by [seasons]); null without it.</summary>
    internal static class Fdm
    {
        private static bool _looked;
        private static MethodInfo _of;

        public static string DungeonOf(Character c)
        {
            if (!_looked)
            {
                _looked = true;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = null;
                    try { t = a.GetType("FiresDungeonMaster.Api.DungeonApi", false); } catch { }
                    if (t != null) { _of = t.GetMethod("DungeonOf", new[] { typeof(Character) }); break; }
                }
                MonsterLog.Debug($"set drops: FDM DungeonApi.DungeonOf {(_of != null ? "found" : "not found (dungeon_boss sets never drop)")}");
            }
            try { return _of != null ? _of.Invoke(null, new object[] { c }) as string : null; } catch { return null; }
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
    internal static class SetDropsDeathPatch
    {
        private static void Prefix(Character __instance)
        {
            try { SetDrops.OnKill(__instance); }
            catch (Exception e) { MonsterLog.Debug($"set drops: OnKill threw {e.GetType().Name}: {e.Message}"); }
        }
    }

    /// <summary>Every peer registers the roll RPC once its ZRoutedRpc exists.</summary>
    [HarmonyPatch(typeof(Game), nameof(Game.Start))]
    internal static class SetDropsRpcPatch
    {
        private static void Postfix() => SetDrops.EnsureRegistered();
    }
}

namespace FiresMonsterOverhaul.Gear
{
    /// <summary>0.0.39 ([ghost] RPC audit 10-20): Blast / Lane / Entangle / Hazard RPCs registered only once an FMO monster
    /// woke on this peer, so a peer with none loaded yet dropped those broadcasts silently. Every new ZRoutedRpc now gets
    /// them at ZNet.Awake (each EnsureRegistered is guarded by the instance it registered on, so this never doubles up).</summary>
    [HarmonyLib.HarmonyPatch(typeof(ZNet), "Awake")]
    internal static class AbilityRpcZNetAwakePatch
    {
        private static void Postfix()
        {
            try { FiresMonsterOverhaul.Abilities.Blast.EnsureRegistered(); } catch (System.Exception e) { MonsterLog.Debug($"rpc: Blast register threw {e.Message}"); }
            try { FiresMonsterOverhaul.Bosses.Entangle.EnsureRegistered(); } catch (System.Exception e) { MonsterLog.Debug($"rpc: Entangle register threw {e.Message}"); }
            try { FiresMonsterOverhaul.Affixes.AffixRunner.EnsureHazardRpc(); } catch (System.Exception e) { MonsterLog.Debug($"rpc: Hazard register threw {e.Message}"); }
        }
    }
}
