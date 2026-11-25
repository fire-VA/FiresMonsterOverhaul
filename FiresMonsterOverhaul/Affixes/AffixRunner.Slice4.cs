using System.Collections;
using FiresCore.Npc.Archetypes.Effects;
using FiresMonsterOverhaul.Casts;
using FiresMonsterOverhaul.Tiers;
using UnityEngine;

namespace FiresMonsterOverhaul.Affixes
{
    /// <summary>
    /// Phase 3 slice 4 (32-PHASE3-PLAN.md; Fire 10-08: "start on the remaining affixes"): Warded, Summoner, Hexer,
    /// Elemental (Molten / Frostbound / Plagued), Spirit-linked. Owner-side ticks here; Warded's immunity and the
    /// spirit link's damage sharing are called from the Character.ApplyDamage patch.
    /// </summary>
    public sealed partial class AffixRunner
    {
        // ZDOID values take string keys in vanilla's ZDO (stored as a key pair), like PackSystem's "fmo_pack".
        public const string ZdoWard = "fmo_ward";                // its totem's ZDOID
        public const string ZdoWardOf = "fmo_ward_of";           // on the totem: its monster
        public static readonly int ZdoWardRaised = "fmo_ward_raised".GetStableHashCode();
        public const string ZdoSummonedBy = "fmo_summoned_by";   // on an add: its summoner
        public static readonly int ZdoElement = "fmo_hazard_elem".GetStableHashCode();   // 0 fire, 1 frost, 2 poison
        public const string ZdoLink = "fmo_link";                // its twin's ZDOID
        public static readonly int ZdoLinkSpawned = "fmo_link_spawned".GetStableHashCode();
        private static readonly int ZdoReviveAt = "fmo_revive_at".GetStableHashCode();
        private static readonly int ZdoRevivePrefab = "fmo_revive_prefab".GetStableHashCode();
        private static readonly int ZdoReviveLevel = "fmo_revive_level".GetStableHashCode();
        private static readonly int ZdoReviveTier = "fmo_revive_tier".GetStableHashCode();
        private static readonly int ZdoReviveAffixes = "fmo_revive_affixes".GetStableHashCode();
        private static readonly int ZdoRevivePos = "fmo_revive_pos".GetStableHashCode();
        private const string RpcLinkDied = "FMO_LinkDied";
        private const string RpcHazard = "FMO_Hazard";
        private const float WardOffset = 4f;
        private const float WardMinHealth = 50f;
        private const float SummonOffset = 2.5f;
        private const float SummonHealth = 0.5f;
        private const float SummonRingRadius = 3f;
        private const float SummonCountRange = 30f;
        private const float HexRange = 20f;
        private const float HexRingRadius = 1.5f;
        private const float LinkOffset = 2f;
        private const float LinkRange = 40f;
        private const float ReviveHealth = 0.5f;
        private const float HazardHeight = 3f;
        private const float HazardTick = 1f;
        /// <summary>Marks a spirit link's shared hit (m_toolTier), so the twin doesn't share it back.</summary>
        internal const short ShareMarker = 32000;

        private static ZRoutedRpc _hazardRpcOn;
        private float _nextSummon;
        private float _nextHex;
        private float _nextHazard;

        private void AwakeSlice4()
        {
            if (_nview != null && _nview.IsValid()) _nview.Register<ZPackage>(RpcLinkDied, RPC_LinkDied);
            EnsureHazardRpc();
        }

        private void UpdateSlice4(ZDO zdo, MonsterAffix affixes, bool inCombat)
        {
            if ((affixes & MonsterAffix.Elemental) != 0 && MonsterConfig.Elemental.Value) EnsureElement(zdo);
            if ((affixes & MonsterAffix.SpiritLinked) != 0 && MonsterConfig.SpiritLinked.Value) EnsureTwin(zdo);
            if ((affixes & MonsterAffix.Warded) != 0) CheckWard(zdo);
            if (!inCombat) return;
            if ((affixes & MonsterAffix.Warded) != 0 && MonsterConfig.Warded.Value) RaiseWard(zdo);
            if ((affixes & MonsterAffix.Summoner) != 0 && MonsterConfig.Summoner.Value) TrySummon(zdo);
            if ((affixes & MonsterAffix.Hexer) != 0 && MonsterConfig.Hexer.Value) TryHex();
            if ((affixes & MonsterAffix.Elemental) != 0 && MonsterConfig.Elemental.Value) TryHazard(zdo);
        }

        private string Prefab => Utils.GetPrefabName(gameObject);

        /// <summary>A copy of this creature beside it: no tier roll, no creature setup (same multipliers), no drops.</summary>
        private Character SpawnCopy(string prefab, Vector3 at, int level) =>
            Abilities.Adds.SpawnCopy(_character, prefab, at, level, transform.rotation);

        // ---------------------------------------------------------------- Warded

        // Once, when it first fights: a ward totem a few metres away.
        private void RaiseWard(ZDO zdo)
        {
            if (zdo.GetBool(ZdoWardRaised)) return;
            zdo.Set(ZdoWardRaised, true);
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(WardTotem.PrefabName) : null;
            if (prefab == null) { MonsterLog.Warning($"affix: {Prefab} Warded but no ward totem prefab: it stays unwarded"); return; }
            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector3 at = transform.position + new Vector3(dir.x, 0f, dir.y) * WardOffset;
            RingUp.Ground(ref at, 0f, "fmo spawn");   // 0.0.46: the ring floor on a halo ring, else the heightmap as before
            var go = Object.Instantiate(prefab, at, RingUp.Upright(at, Random.Range(0f, 360f)));   // 0.0.46: upright to a halo ring
            var nv = go.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            float health = Mathf.Max(WardMinHealth, _character.GetMaxHealth() * MonsterConfig.WardHealth.Value);
            nv.GetZDO().Set(ZdoWardOf, zdo.m_uid);
            nv.GetZDO().Set(ZDOVars.s_health, health);
            zdo.Set(ZdoWard, nv.GetZDO().m_uid);
            MonsterLog.Debug($"affix: {Prefab} raised a ward totem at {at:F1} ({health:0} HP; immune within {MonsterConfig.WardRange.Value:0} m while it stands) totem {nv.GetZDO().m_uid} {zdo.m_uid}");
        }

        private void CheckWard(ZDO zdo)
        {
            var id = zdo.GetZDOID(ZdoWard);
            if (id == ZDOID.None || ZDOMan.instance == null || ZDOMan.instance.GetZDO(id) != null) return;
            zdo.Set(ZdoWard, ZDOID.None);
            MonsterLog.Debug($"affix: {Prefab} ward broken (its totem is gone): it takes damage again {zdo.m_uid}");
        }

        /// <summary>On the monster's owner, from the ApplyDamage prefix: immune while its totem stands within range.</summary>
        public static bool WardHolds(Character c)
        {
            if (c == null || c.IsPlayer() || !MonsterConfig.Warded.Value || ZDOMan.instance == null) return false;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return false;
            var zdo = nv.GetZDO();
            if ((MonsterTierTag.AffixesOf(zdo) & MonsterAffix.Warded) == 0) return false;
            var totem = TotemOf(zdo);
            return totem != null && Vector3.Distance(totem.GetPosition(), c.transform.position) <= MonsterConfig.WardRange.Value;
        }

        public static ZDO TotemOf(ZDO zdo)
        {
            var id = zdo.GetZDOID(ZdoWard);
            return id == ZDOID.None || ZDOMan.instance == null ? null : ZDOMan.instance.GetZDO(id);
        }

        // Its totem crumbles with it.
        private static void DestroyWard(ZDO zdo)
        {
            var totem = TotemOf(zdo);
            if (totem == null) return;
            totem.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(totem);
        }

        // ---------------------------------------------------------------- Summoner

        private int AliveAdds(ZDO zdo)
        {
            Scan.Clear();
            Character.GetCharactersInRange(transform.position, SummonCountRange, Scan);
            int n = 0;
            foreach (var c in Scan)
            {
                var nv = c != null && !c.IsDead() ? c.GetComponent<ZNetView>() : null;
                if (nv != null && nv.IsValid() && nv.GetZDO().GetZDOID(ZdoSummonedBy) == zdo.m_uid) n++;
            }
            return n;
        }

        // An interruptible call; on completion weaker copies of itself (half health, level 1, no drops).
        private void TrySummon(ZDO zdo)
        {
            if (Time.time < _nextSummon) return;
            if (AliveAdds(zdo) >= MonsterConfig.SummonMaxAlive.Value) return;
            if (_cast == null) _cast = GetComponent<MonsterCast>() ?? Patches.HoldWhileCastingPatch.Added(gameObject.AddComponent<MonsterCast>());
            if (_cast.Active) return;
            float seconds = MonsterConfig.SummonCastSeconds.Value;
            string prefab = Prefab;
            if (!_cast.Begin("summon", seconds, true, 2, SummonRingRadius, () => Summon(prefab), holdStill: true)) return;
            _nextSummon = Time.time + MonsterConfig.SummonCooldown.Value;
            MonsterLog.Debug($"affix: {prefab} summon call ({seconds:0.0} s, interruptible) {zdo.m_uid}");
        }

        private void Summon(string prefab)
        {
            var zdo = _nview.GetZDO();
            int count = Mathf.Min(MonsterConfig.SummonCount.Value, MonsterConfig.SummonMaxAlive.Value - AliveAdds(zdo));
            var made = new System.Collections.Generic.List<string>();
            for (int i = 0; i < count; i++)
            {
                Vector2 off = Random.insideUnitCircle.normalized * SummonOffset;
                var add = SpawnCopy(prefab, transform.position + new Vector3(off.x, 0f, off.y), 1);
                if (add == null) continue;
                var nv = add.GetComponent<ZNetView>();
                nv.GetZDO().Set(ZdoSummonedBy, zdo.m_uid);
                nv.GetZDO().Set(Setup.CreatureSetup.ZdoHealthMult, nv.GetZDO().GetFloat(Setup.CreatureSetup.ZdoHealthMult, 1f) * SummonHealth);
                add.SetLevel(1);
                add.SetHealth(add.GetMaxHealth());
                made.Add($"{prefab}={nv.GetZDO().m_uid}");
            }
            MonsterLog.Debug($"affix: {prefab} summoned {made.Count} add(s) ({string.Join(" ", made)}; half health, no drops) {zdo.m_uid}");
        }

        // ---------------------------------------------------------------- Hexer

        // An interruptible cast with a ring on its target; on completion a curse (less damage dealt, slower).
        private void TryHex()
        {
            if (Time.time < _nextHex || _ai == null) return;
            var target = _ai.m_targetCreature;
            if (target == null || target.IsDead() || !(target.IsPlayer() || target.IsTamed())) return;
            if (Vector3.Distance(transform.position, target.transform.position) > HexRange || !_ai.CanSeeTarget(target)) return;
            if (_cast == null) _cast = GetComponent<MonsterCast>() ?? Patches.HoldWhileCastingPatch.Added(gameObject.AddComponent<MonsterCast>());
            if (_cast.Active) return;
            float seconds = MonsterConfig.HexCastSeconds.Value;
            string prefab = Prefab;
            if (!_cast.Begin("hex", seconds, true, 3, HexRingRadius, () =>
            {
                if (target == null || target.IsDead()) return;
                target.GetSEMan().AddStatusEffect(HexEffect.Hash, true);
                MonsterLog.Debug($"affix: {prefab} hexed {target.GetHoverName()} for {MonsterConfig.HexSeconds.Value:0} s (-{MonsterConfig.HexDamagePenalty.Value:P0} damage, -{MonsterConfig.HexSlow.Value:P0} speed) {_nview.GetZDO().m_uid}");
            }, holdStill: true, ringCenter: target.transform.position)) return;
            _nextHex = Time.time + MonsterConfig.HexCooldown.Value;
            MonsterLog.Debug($"affix: {prefab} hex cast on {target.GetHoverName()} ({seconds:0.0} s, interruptible) {_nview.GetZDO().m_uid}");
        }

        // ---------------------------------------------------------------- Elemental (Molten / Frostbound / Plagued)

        // The element follows the biome it appeared in: frost in the mountains and the north, poison in the swamp,
        // fire in the Ashlands, otherwise any.
        private void EnsureElement(ZDO zdo)
        {
            if (zdo.GetInt(ZdoElement, -1) >= 0) return;
            var biome = Setup.CreatureSetup.BiomeAt(Setup.CreatureSetup.SpawnPoint(_character));
            int element = biome == Heightmap.Biome.Mountain || biome == Heightmap.Biome.DeepNorth ? 1
                : biome == Heightmap.Biome.Swamp ? 2
                : biome == Heightmap.Biome.AshLands ? 0
                : Random.Range(0, MonsterAffixInfo.ElementLabels.Length);
            zdo.Set(ZdoElement, element);
        }

        public static string ElementLabel(ZDO zdo)
        {
            int e = zdo.GetInt(ZdoElement, -1);
            return e >= 0 && e < MonsterAffixInfo.ElementLabels.Length ? MonsterAffixInfo.ElementLabels[e] : MonsterAffixInfo.Label(MonsterAffix.Elemental);
        }

        private void TryHazard(ZDO zdo)
        {
            if (Time.time < _nextHazard || ZRoutedRpc.instance == null) return;
            _nextHazard = Time.time + MonsterConfig.HazardInterval.Value;
            EnsureElement(zdo);
            int element = zdo.GetInt(ZdoElement, 0);
            Vector3 at = transform.position;
            float radius = MonsterConfig.HazardRadius.Value;
            float seconds = MonsterConfig.HazardSeconds.Value;
            float dps = _character.GetMaxHealth() * MonsterConfig.HazardDamage.Value;
            EnsureHazardRpc();
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcHazard, at, radius, seconds, dps, element);
            MonsterLog.Debug($"affix: {Prefab} {ElementLabel(zdo).ToLowerInvariant()} ground at {at:F1} (r {radius:0.#} m, {seconds:0} s, {dps:0.#}/s) {zdo.m_uid}");
        }

        /// <summary>0.0.40 (boss phases): the Elemental ring, broadcast by a boss script (element 0 fire, 1 frost, 2 poison).</summary>
        internal static void BroadcastHazard(Vector3 at, float radius, float seconds, float dps, int element)
        {
            if (ZRoutedRpc.instance == null) return;
            EnsureHazardRpc();
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcHazard, at, radius, seconds, dps, element);
        }

        internal static void EnsureHazardRpc()   // 0.0.39: internal for the ZNet.Awake registration
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc == _hazardRpcOn) return;
            rpc.Register<Vector3, float, float, float, int>(RpcHazard, OnHazard);
            _hazardRpcOn = rpc;
        }

        private static void OnHazard(long sender, Vector3 pos, float radius, float seconds, float dps, int element)
        {
            if (Player.m_localPlayer == null || Plugin.Instance == null) return; // a dedicated server has nobody to burn
            GroundRingManager.Show(GroundRingKind.Damage, pos, radius, seconds, 0L, false);
            Plugin.Instance.StartCoroutine(HazardTicks(pos, radius, seconds, dps, element));
        }

        // Each client burns / chills / poisons its own player while it stands in the ring: vanilla elemental damage, so
        // vanilla's Burning / Frost / Poison effects (and their cures) follow.
        private static IEnumerator HazardTicks(Vector3 pos, float radius, float seconds, float dps, int element)
        {
            bool logged = false;
            for (float t = 0f; t < seconds; t += HazardTick)
            {
                yield return new WaitForSeconds(HazardTick);
                var player = Player.m_localPlayer;
                if (player == null || player.IsDead()) continue;
                Vector3 d = player.transform.position - pos;
                if (new Vector2(d.x, d.z).magnitude > radius || Mathf.Abs(d.y) > HazardHeight) continue;
                var hit = new HitData();
                if (element == 1) hit.m_damage.m_frost = dps;
                else if (element == 2) hit.m_damage.m_poison = dps;
                else hit.m_damage.m_fire = dps;
                hit.m_point = player.GetCenterPoint();
                hit.m_dir = RingUp.Up(player.transform.position);   // 0.0.46: along a halo ring's up
                hit.m_hitType = HitData.HitType.EnemyHit;
                player.Damage(hit);
                if (logged) continue;
                logged = true;
                string name = element >= 0 && element < MonsterAffixInfo.ElementLabels.Length ? MonsterAffixInfo.ElementLabels[element] : "Elemental";
                MonsterLog.Debug($"affix: {name.ToLowerInvariant()} ground at {pos:F1} hit {player.GetPlayerName()} ({dps:0.#}/s while inside)");
            }
        }

        // ---------------------------------------------------------------- Spirit-linked

        // Once: a twin beside it; the two are linked both ways.
        private void EnsureTwin(ZDO zdo)
        {
            if (zdo.GetBool(ZdoLinkSpawned)) return;
            zdo.Set(ZdoLinkSpawned, true);
            if (zdo.GetZDOID(ZdoLink) != ZDOID.None) return; // a revived twin is already linked
            var twin = SpawnCopy(Prefab, transform.position + transform.right * LinkOffset, _character.GetLevel());
            if (twin == null) return;
            var tz = twin.GetComponent<ZNetView>().GetZDO();
            tz.Set(ZdoLink, zdo.m_uid);
            tz.Set(ZdoLinkSpawned, true);
            zdo.Set(ZdoLink, tz.m_uid);
            MonsterLog.Debug($"affix: {Prefab} spirit link with its twin {Prefab}={tz.m_uid} {zdo.m_uid}");
        }

        public static bool Linked(ZDO zdo) => zdo.GetZDOID(ZdoLink) != ZDOID.None;

        /// <summary>
        /// On the victim's owner, from the ApplyDamage prefix: a linked creature sends Spirit Link Share of the hit to
        /// its twin (if the twin is loaded here and near) and keeps the rest. Returns what it keeps.
        /// </summary>
        public static float SpiritShare(Character c, HitData hit, float through)
        {
            if (c == null || hit == null || through <= 0f || c.IsPlayer() || !MonsterConfig.SpiritLinked.Value) return through;
            if (hit.m_toolTier == ShareMarker || ZNetScene.instance == null) return through;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid() || !nv.IsOwner()) return through;
            var id = nv.GetZDO().GetZDOID(ZdoLink);
            if (id == ZDOID.None) return through;
            var go = ZNetScene.instance.FindInstance(id);
            var twin = go != null ? go.GetComponent<Character>() : null;
            if (twin == null || twin.IsDead() || Vector3.Distance(twin.transform.position, c.transform.position) > LinkRange) return through;
            float share = through * MonsterConfig.SpiritLinkShare.Value;
            var shared = new HitData();
            shared.m_damage.m_damage = share;
            shared.m_toolTier = ShareMarker;
            shared.m_attacker = hit.m_attacker;
            shared.m_point = twin.GetCenterPoint();
            shared.m_dir = hit.m_dir;
            shared.m_hitType = hit.m_hitType;
            twin.Damage(shared);
            MonsterLog.Debug($"affix: {Utils.GetPrefabName(c.gameObject)} spirit link sent {share:0.0} of {through:0.0} to its twin {id}");
            return through - share;
        }

        // From the death prefix: the twin's owner is told, and brings this one back unless it dies too in time.
        private static void NotifyLinkDied(Character c, ZDO zdo)
        {
            var id = zdo.GetZDOID(ZdoLink);
            if (id == ZDOID.None || ZDOMan.instance == null || ZRoutedRpc.instance == null) return;
            var twin = ZDOMan.instance.GetZDO(id);
            if (twin == null) return;
            var pkg = new ZPackage();
            pkg.Write(c.transform.position);
            pkg.Write(Utils.GetPrefabName(c.gameObject));
            pkg.Write(c.GetLevel());
            pkg.Write((int)MonsterTierTag.TierOf(zdo));
            pkg.Write((int)MonsterTierTag.AffixesOf(zdo));
            ZRoutedRpc.instance.InvokeRoutedRPC(twin.GetOwner(), id, RpcLinkDied, pkg);
        }

        private void RPC_LinkDied(long sender, ZPackage pkg)
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _character == null || _character.IsDead()) return;
            var zdo = _nview.GetZDO();
            Vector3 pos = pkg.ReadVector3();
            string prefab = pkg.ReadString();
            int level = pkg.ReadInt();
            int tier = pkg.ReadInt();
            int affixes = pkg.ReadInt();
            float seconds = MonsterConfig.SpiritLinkReviveSeconds.Value;
            zdo.Set(ZdoReviveAt, Now() + (long)(seconds * TicksPerSecond));
            zdo.Set(ZdoRevivePos, pos);
            zdo.Set(ZdoRevivePrefab, prefab);
            zdo.Set(ZdoReviveLevel, level);
            zdo.Set(ZdoReviveTier, tier);
            zdo.Set(ZdoReviveAffixes, affixes);
            zdo.Set(ZdoLink, ZDOID.None);
            MonsterLog.Debug($"affix: {Prefab} spirit link: its twin {prefab} died; it comes back in {seconds:0} s unless this one dies first {zdo.m_uid}");
        }

        private void ReviveIfDue(ZDO zdo)
        {
            long at = zdo.GetLong(ZdoReviveAt);
            if (at == 0L || Now() < at) return;
            zdo.Set(ZdoReviveAt, 0L);
            string prefab = zdo.GetString(ZdoRevivePrefab);
            var revived = SpawnCopy(prefab, zdo.GetVec3(ZdoRevivePos, transform.position), zdo.GetInt(ZdoReviveLevel, 1));
            if (revived == null) return;
            var rz = revived.GetComponent<ZNetView>().GetZDO();
            int tier = zdo.GetInt(ZdoReviveTier);
            int affixes = zdo.GetInt(ZdoReviveAffixes);
            if (tier != 0) rz.Set(MonsterTierTag.ZdoTier, tier);
            if (affixes != 0) rz.Set(MonsterTierTag.ZdoAffixes, affixes);
            rz.Set(ZdoLink, zdo.m_uid);
            rz.Set(ZdoLinkSpawned, true);
            zdo.Set(ZdoLink, rz.m_uid);
            revived.SetHealth(revived.GetMaxHealth() * ReviveHealth);
            MonsterLog.Debug($"affix: {Prefab} spirit link revived its twin {prefab}={rz.m_uid} at {ReviveHealth:P0} health (no drops) {zdo.m_uid}");
        }

        /// <summary>From the death prefix on the dying creature's owner: its ward totem crumbles, its twin is told.</summary>
        private static void OnDeathSlice4(Character c, ZDO zdo)
        {
            if ((MonsterTierTag.AffixesOf(zdo) & MonsterAffix.Warded) != 0) DestroyWard(zdo);
            NotifyLinkDied(c, zdo);
        }
    }
}
