using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FiresMonsterOverhaul.Gear
{
    /// <summary>
    /// Monster gear, Phase 1 (Tools\FMO_MONSTER_GEAR_DESIGN.md, locked by Fire 10-13): Draugr and Skeletons wear real player
    /// ARMOUR, rolled once per creature on its owner from the biome's set, quality from the private-key boss tier near the
    /// spawn, rarity/affixes from Fires Loot Overhaul's own roll (FloApi.Roll, Veteran and up only) when FLO is installed.
    ///  - Only slots the body can show are filled (Draugr: helmet, chest, legs; Skeleton: helmet, chest): no hidden stats.
    ///  - Worn armour reduces damage taken: FLO's ApplyTo does it when present, else vanilla's armour formula here
    ///    (vanilla gives a non-player NO armour from worn items: Character.GetBodyArmor is 0).
    ///  - A creature's inventory isn't saved, so the gear is kept in the creature's ZDO ("fmo_gear") and re-equipped on load
    ///    and on an owner change; the visuals reach every peer through vanilla VisEquipment's ZDO hashes, fitted onto the
    ///    body by Core's armour rebind.
    ///  - On death each worn piece drops with [12 - Monster Gear] Drop Chance (Champions: Champion Drop Chance), as the same
    ///    item (FLO data kept) at Drop Durability of its maximum. Never from tames or bosses.
    /// Weapons stay the monster's own in Phase 1: a player weapon's attack animations don't exist on monster animators.
    /// </summary>
    internal sealed class MonsterGear : MonoBehaviour
    {
        public static readonly int ZdoGear = "fmo_gear".GetStableHashCode();
        public static readonly int ZdoGearDone = "fmo_gear_done".GetStableHashCode();
        private const char EntrySep = ';', FieldSep = '|';

        private Humanoid _body;
        private ZNetView _nview;
        private float _next;
        private readonly List<ItemDrop.ItemData> _worn = new List<ItemDrop.ItemData>();
        internal IReadOnlyList<ItemDrop.ItemData> Worn => _worn;

        // which slots each body may show (Fire Q2: no stats from slots that can't be drawn)
        private static readonly Dictionary<string, ItemDrop.ItemData.ItemType[]> Slots = new Dictionary<string, ItemDrop.ItemData.ItemType[]>
        {
            { "Draugr", new[] { ItemDrop.ItemData.ItemType.Helmet, ItemDrop.ItemData.ItemType.Chest, ItemDrop.ItemData.ItemType.Legs } },
            { "Skeleton", new[] { ItemDrop.ItemData.ItemType.Helmet, ItemDrop.ItemData.ItemType.Chest } },
        };

        public static bool EligiblePrefab(string prefab, bool ignoreEnabled = false)
        {
            if ((!ignoreEnabled && !MonsterConfig.GearEnabled.Value) || string.IsNullOrEmpty(prefab)) return false;
            foreach (var p in (MonsterConfig.GearPrefabs.Value ?? "").Split(','))
                if (p.Trim().Equals(prefab, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string BodyOf(string prefab) => prefab.StartsWith("Draugr", StringComparison.OrdinalIgnoreCase) ? "Draugr" : prefab.StartsWith("Skeleton", StringComparison.OrdinalIgnoreCase) ? "Skeleton" : null;

        private void Awake()
        {
            _body = GetComponent<Humanoid>();
            _nview = GetComponent<ZNetView>();
            _next = Time.time + 1.5f;   // after Humanoid.Start's default items and CreatureSetup (spawn + 1 frame)
        }

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 2f;
            if (_body == null || _nview == null || !_nview.IsValid() || !_nview.IsOwner() || _body.IsDead()) return;
            var zdo = _nview.GetZDO();
            if (!zdo.GetBool(ZdoGearDone)) { zdo.Set(ZdoGearDone, true); if (ShouldRoll()) Roll(zdo, false); }
            EnsureEquipped(zdo);
        }

        private bool ShouldRoll()
        {
            if (_body.IsTamed() || _body.IsBoss() || !Tiers.MonsterTierTag.Eligible(_body, _nview)) return false;   // tames, bosses, possessed
            return UnityEngine.Random.value < MonsterConfig.GearChance.Value;
        }

        // ── rolling ──────────────────────────────────────────────────────────────────────────────────────
        /// <summary>Rolls this creature's gear (owner). force: every allowed slot, ignoring the chance (fmo_gear, tests).</summary>
        internal string Roll(ZDO zdo, bool force)
        {
            string prefab = Utils.GetPrefabName(gameObject), body = BodyOf(prefab);
            if (body == null || ObjectDB.instance == null) return "not a gear body";
            var tier = Tiers.MonsterTierTag.TierOf(zdo);
            var range = PiecesFor(tier);
            int pieces = force ? 99 : UnityEngine.Random.Range(range.min, range.max + 1);
            if (pieces <= 0) { zdo.Set(ZdoGear, ""); return "rolled 0 pieces"; }
            Vector3 pos = Setup.CreatureSetup.SpawnPoint(_body);
            var biome = Setup.CreatureSetup.BiomeAt(pos);
            int bossTier = Setup.CreatureSetup.BossLevel(pos);
            int quality = Mathf.Max(1, 1 + bossTier / 2);   // 0.0.31 (Fire 10-14): no flat q4 cap; Make clamps to the item's own max quality
            var set = SetFor(biome);
            var slots = Slots[body].OrderBy(_ => UnityEngine.Random.value).Take(pieces).ToList();
            var entries = new List<string>();
            var said = new List<string>();
            foreach (var slot in slots)
            {
                string itemName = set.FirstOrDefault(n => TypeOf(n) == slot);
                if (itemName == null) continue;
                var item = Make(itemName, quality);
                if (item == null) continue;
                string flo = "";
                if (tier >= Tiers.MonsterTier.Veteran && Flo.Available)
                {
                    float luck = LuckFor(tier);
                    if (Flo.Roll(item, -1, Mathf.Clamp((int)tier, 1, 3), biome, luck)) flo = Flo.Serialize(item) ?? "";   // FLO 0.1.2: null = no FLO data
                }
                entries.Add(string.Join(FieldSep.ToString(), new[] { itemName, quality.ToString(), Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(flo ?? "")) }));
                said.Add($"{itemName}@q{quality}{(flo != "" ? " FLO" : "")}");
            }
            zdo.Set(ZdoGear, string.Join(EntrySep.ToString(), entries));
            _worn.Clear();   // re-equip from the string
            string line = $"gear: {prefab} {tier} in {biome} (boss tier {bossTier}) rolled {said.Count}: {string.Join(", ", said)} {zdo.m_uid}";
            MonsterLog.Debug(line);
            return line;
        }

        private static (int min, int max) PiecesFor(Tiers.MonsterTier tier)
        {
            var parts = (MonsterConfig.GearPiecesByTier.Value ?? "").Split(',');
            int i = Mathf.Clamp((int)tier, 0, 3);
            if (parts.Length > i)
            {
                var mm = parts[i].Split('-');
                if (mm.Length == 2 && int.TryParse(mm[0], out int a) && int.TryParse(mm[1], out int b)) return (Mathf.Max(0, a), Mathf.Max(a, b));
            }
            return (0, 1);
        }

        private static float LuckFor(Tiers.MonsterTier tier)
        {
            var parts = (MonsterConfig.GearFloLuckByTier.Value ?? "").Split(',');
            int i = Mathf.Clamp((int)tier, 0, 3);
            return parts.Length > i && float.TryParse(parts[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float l) ? l : 1f;
        }

        private static string[] SetFor(Heightmap.Biome biome)
        {
            foreach (var part in (MonsterConfig.GearTables.Value ?? "").Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && Enum.TryParse(kv[0].Trim(), true, out Heightmap.Biome b) && (biome & b) != 0)
                    return kv[1].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            }
            return new string[0];
        }

        private static ItemDrop.ItemData.ItemType TypeOf(string itemName)
        {
            var drop = ObjectDB.instance?.GetItemPrefab(itemName)?.GetComponent<ItemDrop>();
            return drop != null ? drop.m_itemData.m_shared.m_itemType : ItemDrop.ItemData.ItemType.None;
        }

        private static ItemDrop.ItemData Make(string itemName, int quality)
        {
            var go = ObjectDB.instance?.GetItemPrefab(itemName);
            var drop = go != null ? go.GetComponent<ItemDrop>() : null;
            if (drop == null) { MonsterLog.Debug($"gear: no item prefab {itemName}"); return null; }
            var item = drop.m_itemData.Clone();
            item.m_dropPrefab = go;
            item.m_quality = Mathf.Clamp(quality, 1, Mathf.Max(1, item.m_shared.m_maxQuality));
            item.m_durability = item.GetMaxDurability();
            item.m_stack = 1;
            return item;
        }

        // ── equipping (owner; every load and every owner change) ─────────────────────────────────────────────
        private void EnsureEquipped(ZDO zdo)
        {
            string data = zdo.GetString(ZdoGear, "");
            if (string.IsNullOrEmpty(data)) return;
            if (_worn.Count > 0 && _worn.All(i => i != null && _body.IsItemEquiped(i))) return;
            _worn.Clear();
            int quality;
            foreach (var entry in data.Split(EntrySep))
            {
                var f = entry.Split(FieldSep);
                if (f.Length < 3 || !int.TryParse(f[1], out quality)) continue;
                var item = Make(f[0], quality);
                if (item == null) continue;
                string flo = "";
                try { flo = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(f[2])); } catch { }
                if (flo.Length > 0) Flo.Deserialize(item, flo);
                var inv = _body.GetInventory();
                if (inv == null) return;
                inv.AddItem(item);
                _body.EquipItem(item, false);
                _worn.Add(item);
            }
            if (Flo.Available) Flo.ApplyTo(_body);
            ArmorCache.Invalidate(_body);
            MonsterLog.Debug($"gear: {Utils.GetPrefabName(gameObject)} equips {_worn.Count} piece(s) ({string.Join(", ", _worn.Select(i => $"{i.m_dropPrefab?.name}@q{i.m_quality}"))}), armour {ArmorCache.Of(this):0} {zdo.m_uid}");
        }

        /// <summary>The summed armour of the worn pieces (for the no-FLO armour path), cached until the gear changes.</summary>
        internal float Armor() => _worn.Where(i => i != null && _body.IsItemEquiped(i)).Sum(i => i.GetArmor());

        // ── death drops (owner) ──────────────────────────────────────────────────────────────────────────────
        internal void DropOnDeath()
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _worn.Count == 0) return;
            if (_body.IsTamed()) return;
            var zdo = _nview.GetZDO();
            bool champion = Tiers.MonsterTierTag.TierOf(zdo) == Tiers.MonsterTier.Champion;
            float chance = champion ? MonsterConfig.GearDropChanceChampion.Value : MonsterConfig.GearDropChance.Value;
            foreach (var item in _worn.ToList())
            {
                if (item == null) continue;
                float roll = UnityEngine.Random.value;
                if (roll >= chance) continue;
                var copy = item.Clone();
                copy.m_durability = copy.GetMaxDurability() * Mathf.Clamp01(MonsterConfig.GearDropDurability.Value);
                ItemDrop.DropItem(copy, 1, transform.position + RingUp.Up(transform.position) * 0.8f, Quaternion.identity);   // 0.0.46
                MonsterLog.Debug($"gear drop: {Utils.GetPrefabName(gameObject)} dropped {copy.m_dropPrefab?.name}@q{copy.m_quality} (roll {roll:0.000} < {chance:0.000}) {zdo.m_uid}");
            }
            zdo.Set(ZdoGear, "");
        }

        // ── Core 0.2.421+: MonsterArmorFit fits worn armour onto these bodies inside VisEquipment, on every client ─────────
        /// <summary>Registers every [12 - Monster Gear] Prefabs body with Core's MonsterArmorFit (reflection: a no-op on an
        /// older Core). Called once at plugin load; it only stores names.</summary>
        public static void RegisterBodies()
        {
            MethodInfo reg = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = a.GetType("FiresCore.Visuals.MonsterArmorFit", false); } catch { }
                if (t != null) { reg = t.GetMethod("RegisterPrefab", new[] { typeof(string), typeof(string) }); break; }
            }
            if (reg == null) { MonsterLog.Debug("gear: Core has no MonsterArmorFit (older than 0.2.421): worn armour won't be fitted onto monster bodies"); return; }
            var done = new List<string>();
            foreach (var p in (MonsterConfig.GearPrefabs.Value ?? "").Split(','))
            {
                string name = p.Trim();
                if (name.Length == 0) continue;
                try { if (reg.Invoke(null, new object[] { name, null }) is bool ok && ok) done.Add(name); } catch (Exception e) { MonsterLog.Debug($"gear: MonsterArmorFit.RegisterPrefab({name}) threw {e.GetType().Name}"); }
            }
            MonsterLog.Debug($"gear: registered {done.Count} body prefab(s) with Core MonsterArmorFit: {string.Join(", ", done)}");
        }

        /// <summary>Fire 10-13 Q2: pre-build Core's refit cache at world load, so the first geared monster of each kind doesn't
        /// cost a full fit mid-fight. Core's MonsterArmorFit.Prewarm(string monsterPrefab, params string[] itemPrefabs), by
        /// reflection (a no-op on a Core without it, or on a dedicated server where Core skips the fit). Every body prefab gets
        /// every armour item of every Gear Tables biome, but only the slots that body can show.</summary>
        public static void PrewarmFits()
        {
            if (!MonsterConfig.GearEnabled.Value) return;   // off: nothing will be worn, so nothing to warm
            MethodInfo pre = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = a.GetType("FiresCore.Visuals.MonsterArmorFit", false); } catch { }
                if (t != null) { pre = t.GetMethod("Prewarm", new[] { typeof(string), typeof(string[]) }); break; }
            }
            if (pre == null) { MonsterLog.Debug("gear: Core has no MonsterArmorFit.Prewarm: the refit cache fills on first sight"); return; }
            var items = (MonsterConfig.GearTables.Value ?? "").Split(';').Select(p => p.Split('=')).Where(kv => kv.Length == 2)
                .SelectMany(kv => kv[1].Split(',')).Select(s => s.Trim()).Where(s => s.Length > 0).Distinct().ToList();
            int calls = 0;
            foreach (var p in (MonsterConfig.GearPrefabs.Value ?? "").Split(','))
            {
                string prefab = p.Trim(), body = BodyOf(prefab);
                if (body == null) continue;
                var allowed = items.Where(i => Slots[body].Contains(TypeOf(i))).ToArray();
                if (allowed.Length == 0) continue;
                try { pre.Invoke(null, new object[] { prefab, allowed }); calls++; } catch (Exception e) { MonsterLog.Debug($"gear: Prewarm({prefab}) threw {e.GetType().Name}"); }
            }
            MonsterLog.Debug($"gear: asked Core to prewarm {calls} body prefab(s) x {items.Count} gear item(s)");
        }

        // ── command: fmo_gear [radius] (cheat; tests) ─────────────────────────────────────────────────────────
        public static void RegisterCommand()
        {
            new Terminal.ConsoleCommand("fmo_gear", "[radius] - Fires Monster Overhaul: roll full monster gear on every eligible creature this peer owns nearby (tests), and print what each wears", args =>
            {
                var me = Player.m_localPlayer;
                if (me == null) return;
                float r = args.Length >= 2 && float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 30f;
                int n = 0;
                foreach (var c in Character.GetAllCharacters())
                {
                    if (c == null || c.IsPlayer() || Vector3.Distance(c.transform.position, me.transform.position) > r) continue;
                    var g = c.GetComponent<MonsterGear>();
                    if (g == null && EligiblePrefab(Utils.GetPrefabName(c.gameObject), true) && c is Humanoid) g = c.gameObject.AddComponent<MonsterGear>();   // tests: works while [12 - Monster Gear] Enabled is off
                    var nv = c.GetComponent<ZNetView>();
                    if (g == null || nv == null || !nv.IsValid()) continue;
                    if (!nv.IsOwner()) nv.ClaimOwnership();
                    nv.GetZDO().Set(ZdoGearDone, true);
                    string line = g.Roll(nv.GetZDO(), true);
                    g.EnsureEquipped(nv.GetZDO());
                    string msg = $"fmo_gear: {line}; worn {g._worn.Count}, armour {g.Armor():0}";
                    args.Context?.AddString(msg);
                    MonsterLog.Info(msg);
                    n++;
                }
                args.Context?.AddString($"fmo_gear: {n} creature(s)");
            }, isCheat: true);
        }
    }

    /// <summary>Per-creature cache of the worn armour sum (FMO perf pattern), invalidated on (re-)equip.</summary>
    internal static class ArmorCache
    {
        private static readonly Dictionary<Humanoid, float> Cache = new Dictionary<Humanoid, float>();
        public static void Invalidate(Humanoid h) { if (h != null) Cache.Remove(h); }
        public static float Of(MonsterGear g)
        {
            var h = g.GetComponent<Humanoid>();
            if (h == null) return 0f;
            if (!Cache.TryGetValue(h, out float a)) { a = g.Armor(); Cache[h] = a; if (Cache.Count > 4096) foreach (var k in Cache.Keys.Where(k => k == null).ToList()) Cache.Remove(k); }
            return a;
        }
    }

    /// <summary>Fires Loot Overhaul's FloApi (design §19, FLO 0.1.1+), found by reflection; everything is a no-op without it.</summary>
    internal static class Flo
    {
        private static bool _looked;
        private static MethodInfo _roll, _apply, _ser, _de, _rollSet, _setIds, _setPieces, _getSet, _setSlots;

        public static bool Available { get { Look(); return _roll != null && _apply != null; } }
        /// <summary>FLO 0.1.2+ set API (design §21): Roll with a set id, SetIds, SetPieces, GetSet.</summary>
        public static bool SetsAvailable { get { Look(); return _rollSet != null && _setIds != null && _setPieces != null && _getSet != null; } }

        private static void Look()
        {
            if (_looked) return;
            _looked = true;
            var t = Type.GetType("FiresLootOverhaul.FloApi, FiresLootOverhaul");
            if (t == null) foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { try { t = a.GetType("FiresLootOverhaul.FloApi", false); } catch { } if (t != null) break; }
            if (t == null) { MonsterLog.Debug("gear: no FiresLootOverhaul.FloApi (plain vanilla gear, FMO armour formula)"); return; }
            _roll = t.GetMethod("Roll", new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(Heightmap.Biome), typeof(float) });
            _apply = t.GetMethod("ApplyTo", new[] { typeof(Humanoid) });
            _ser = t.GetMethod("Serialize", new[] { typeof(ItemDrop.ItemData) });
            _de = t.GetMethod("Deserialize", new[] { typeof(ItemDrop.ItemData), typeof(string) });
            _rollSet = t.GetMethod("Roll", new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(Heightmap.Biome), typeof(float), typeof(string) });
            _setIds = t.GetMethod("SetIds", Type.EmptyTypes);
            _setPieces = t.GetMethod("SetPieces", new[] { typeof(string), typeof(string) });
            _getSet = t.GetMethod("GetSet", new[] { typeof(ItemDrop.ItemData) });
            _setSlots = t.GetMethod("SetSlots", Type.EmptyTypes);   // FLO 0.1.5+: the active set slots (5, or 9 with its ESO-slots switch)
            MonsterLog.Debug($"gear: FloApi found: Roll {_roll != null}, ApplyTo {_apply != null}, Serialize {_ser != null}, Deserialize {_de != null}, sets {_rollSet != null && _setIds != null && _setPieces != null && _getSet != null}");
        }

        public static bool Roll(ItemDrop.ItemData item, int rarity, int tier, Heightmap.Biome biome, float luck)
        { Look(); try { return _roll != null && (bool)_roll.Invoke(null, new object[] { item, rarity, tier, biome, luck }); } catch (Exception e) { MonsterLog.Debug($"gear: FloApi.Roll threw {e.GetType().Name}"); return false; } }
        public static void ApplyTo(Humanoid h) { Look(); try { _apply?.Invoke(null, new object[] { h }); } catch (Exception e) { MonsterLog.Debug($"gear: FloApi.ApplyTo threw {e.GetType().Name}"); } }
        public static string Serialize(ItemDrop.ItemData item) { Look(); try { return _ser != null ? (string)_ser.Invoke(null, new object[] { item }) ?? "" : ""; } catch { return ""; } }
        public static void Deserialize(ItemDrop.ItemData item, string data) { Look(); try { _de?.Invoke(null, new object[] { item, data }); } catch { } }
        public static bool Roll(ItemDrop.ItemData item, int rarity, int tier, Heightmap.Biome biome, float luck, string setId)
        { Look(); try { return _rollSet != null && (bool)_rollSet.Invoke(null, new object[] { item, rarity, tier, biome, luck, setId }); } catch (Exception e) { MonsterLog.Debug($"set drops: FloApi.Roll(setId) threw {e.GetType().Name}"); return false; } }
        public static string[] SetIds() { Look(); try { return _setIds?.Invoke(null, null) as string[] ?? new string[0]; } catch { return new string[0]; } }
        public static string[] SetPieces(string setId, string slot) { Look(); try { return _setPieces?.Invoke(null, new object[] { setId, slot }) as string[] ?? new string[0]; } catch { return new string[0]; } }
        /// <summary>FLO's active set slots; FLO before 0.1.5 has no SetSlots, so its fixed five.</summary>
        public static string[] SetSlots() { Look(); try { if (_setSlots?.Invoke(null, null) is string[] a && a.Length > 0) return a; } catch { } return new[] { "Helmet", "Chest", "Legs", "Cape", "Ring" }; }
        public static string GetSet(ItemDrop.ItemData item) { Look(); try { return _getSet?.Invoke(null, new object[] { item }) as string; } catch { return null; } }
    }

    /// <summary>Without FLO: worn armour reduces damage on a gear-wearing monster, with vanilla's player armour formula.</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    internal static class MonsterGearArmorPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            if (hit == null || __instance == null || __instance.IsPlayer() || Flo.Available) return;
            var g = __instance.GetComponent<MonsterGear>();
            if (g == null || g.Worn.Count == 0) return;
            float armor = ArmorCache.Of(g) * MonsterConfig.GearArmorFactor.Value;
            if (armor > 0f) hit.ApplyArmor(armor);
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
    internal static class MonsterGearDropPatch
    {
        private static void Prefix(Character __instance) => __instance?.GetComponent<MonsterGear>()?.DropOnDeath();
    }

    /// <summary>World load: warm Core's monster-armour refit cache with the gear tables (Fire Q2; Core with Prewarm).</summary>
    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
    internal static class MonsterGearPrewarmPatch
    {
        // ObjectDB may wake after ZNetScene (the order isn't guaranteed): wait for its items, then warm once.
        private static void Postfix() { if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(WhenReady()); }

        private static System.Collections.IEnumerator WhenReady()
        {
            float until = Time.realtimeSinceStartup + 30f;
            while ((ObjectDB.instance == null || ObjectDB.instance.m_items.Count == 0 || ObjectDB.instance.GetItemPrefab("ArmorIronChest") == null) && Time.realtimeSinceStartup < until) yield return null;
            MonsterGear.PrewarmFits();
        }
    }
}