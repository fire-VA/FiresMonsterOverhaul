using System.Collections.Generic;
using FiresCore.Npc;
using HarmonyLib;
using UnityEngine;

namespace FiresMonsterOverhaul.Scaling
{
    /// <summary>
    /// Boss + mini-boss group scaling (Fire 10-12; Tools\FiresMonsters\35-GROUP-SCALING-PLAN.md). On every MonsterAI creature; it acts
    /// only on bosses (IsBoss) and mini bosses (Elites, Champions, Hildir's quest bosses; each a ScalingConfig switch).
    /// On the creature's OWNER every Recount Seconds:
    ///   - the ENGAGED group: players and companions within Engage Radius who hit it or were its target within Engage Memory;
    ///     a companion counts Companion Weight (0.5) with its owner's power; the count holds its highest for Shrink Grace;
    ///   - the group's power: each player's own client sends its PowerScore here (FMO_Power, every 10 s while near a scaled monster);
    ///     unknown = 1.0; power factor = clamp(1 + Power Strength x (mean power - 1), Power Min, Power Max);
    ///   - HP x (1 + HP Per Player x (n-1)) x power factor; damage taken x max(Resist Floor, 1/(1 + Resist Per Player x (n-1)));
    ///     damage dealt x (1 + Damage Per Player x (n-1)) x (1 + Power Damage Share x (power factor - 1)).
    /// The factors go on the ZDO (every peer reads the same), max HP keeps the health PERCENTAGE, and one Info line per change.
    /// Replace Vanilla Group Scaling: vanilla's own per-player factors are cancelled for these creatures (both directions).
    /// All settings are ScalingConfig: server-authoritative, admin-only.
    /// </summary>
    public sealed class GroupScaler : MonoBehaviour
    {
        public static readonly int ZdoOn = "fmo_grp_on".GetStableHashCode();
        public static readonly int ZdoN = "fmo_grp_n".GetStableHashCode();
        public static readonly int ZdoHp = "fmo_grp_hp".GetStableHashCode();
        public static readonly int ZdoDmg = "fmo_grp_dmg".GetStableHashCode();
        public static readonly int ZdoRes = "fmo_grp_res".GetStableHashCode();
        public static readonly int ZdoPower = "fmo_grp_power".GetStableHashCode();
        public static readonly int ZdoBaseMax = "fmo_grp_basemax".GetStableHashCode();
        public static readonly int ZdoDifficulty = "fmo_boss_difficulty".GetStableHashCode();
        public const string RpcPower = "FMO_Power";

        private ZNetView _nview;
        private Character _body;
        private MonsterAI _ai;
        private float _nextTick;
        private readonly Dictionary<string, (float at, long owner, bool companion, ZDOID id)> _engaged = new Dictionary<string, (float, long, bool, ZDOID)>();
        private readonly Dictionary<long, (float power, float at)> _power = new Dictionary<long, (float, float)>();
        private readonly List<(float at, float n)> _recent = new List<(float, float)>();

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _body = GetComponent<Character>();
            _ai = GetComponent<MonsterAI>();
            _nextTick = Time.time + Random.Range(0.5f, 2f);
            if (_nview != null && _nview.IsValid()) _nview.Register<long, float>(RpcPower, RPC_Power);
        }

        /// <summary>A boss, or a mini boss by the server's switches.</summary>
        public static bool Eligible(Character c, ZNetView nv)
        {
            if (c == null || c.IsPlayer() || c.IsTamed() || nv == null || !nv.IsValid()) return false;
            if (c.IsBoss()) return true;
            var tier = Tiers.MonsterTierTag.TierOf(nv.GetZDO());
            if (tier == Tiers.MonsterTier.Champion && ScalingConfig.MiniBossChampions.Value) return true;
            if (tier == Tiers.MonsterTier.Elite && ScalingConfig.MiniBossElites.Value) return true;
            return ScalingConfig.MiniBossHildir.Value && Utils.GetPrefabName(c.gameObject).Contains("Hildir");
        }

        /// <summary>True on every peer once the owner has scaled this creature (the ZDO flag).</summary>
        public static bool IsScaled(Character c)
        {
            var nv = c != null ? c.GetComponent<ZNetView>() : null;
            return nv != null && nv.IsValid() && ScalingConfig.Enabled.Value && nv.GetZDO().GetBool(ZdoOn);
        }

        public static float GroupSizeOf(ZNetView nv) => nv != null && nv.IsValid() ? Mathf.Max(1f, nv.GetZDO().GetFloat(ZdoN, 1f)) : 1f;
        public static int DifficultyOf(ZNetView nv) => nv != null && nv.IsValid() ? nv.GetZDO().GetInt(ZdoDifficulty, 0) : 0;

        /// <summary>From the ApplyDamage prefix (owner): this attacker is in the fight.</summary>
        internal void NoteEngaged(Character attacker)
        {
            if (attacker == null) return;
            if (attacker is Player p) { _engaged["p:" + p.GetPlayerID()] = (Time.time, p.GetPlayerID(), false, ZDOID.None); return; }
            var comp = attacker.GetComponent<CompanionController>();
            var nv = attacker.GetComponent<ZNetView>();
            if (comp != null && comp.ownerPlayerId != 0 && nv != null && nv.IsValid())
                _engaged["c:" + nv.GetZDO().m_uid] = (Time.time, comp.ownerPlayerId, true, nv.GetZDO().m_uid);
        }

        private void RPC_Power(long sender, long playerId, float power)
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            _power[playerId] = (Mathf.Clamp(power, 0.2f, 20f), Time.time);   // matches PowerScore's 20x read cap (0.0.31)
        }

        private void Update()
        {
            long perfT = FiresMonsterOverhaul.Perf.FmoPerf.Start();
            try { UpdateBody(); }
            finally { FiresMonsterOverhaul.Perf.FmoPerf.Stop(FiresMonsterOverhaul.Perf.FmoPerf.Path.GroupScaler, perfT); }
        }

        private void UpdateBody()
        {
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + Mathf.Max(0.5f, ScalingConfig.RecountSeconds.Value);
            if (!MonsterConfig.Enabled.Value || !ScalingConfig.Enabled.Value || _nview == null || !_nview.IsValid() || !_nview.IsOwner() || _body == null || _body.IsDead()) return;
            if (!Eligible(_body, _nview)) return;
            var zdo = _nview.GetZDO();
            if (!zdo.GetBool(ZdoOn)) zdo.Set(ZdoOn, true);
            Recount(zdo);
        }

        private void Recount(ZDO zdo)
        {
            var target = _ai != null ? _ai.m_targetCreature : null;
            if (target != null && !target.IsDead()) NoteEngaged(target);

            float radius = ScalingConfig.EngageRadius.Value, memory = ScalingConfig.EngageMemory.Value, cw = ScalingConfig.CompanionWeight.Value;
            float n = 0f, powerSum = 0f, weightSum = 0f;
            int players = 0, companions = 0;
            var gone = new List<string>();
            foreach (var pair in _engaged)
            {
                if (Time.time - pair.Value.at > memory) { gone.Add(pair.Key); continue; }
                Character who = null;
                if (!pair.Value.companion) who = Player.GetPlayer(pair.Value.owner);
                else
                {
                    var go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(pair.Value.id) : null;
                    who = go != null ? go.GetComponent<Character>() : null;
                }
                if (who == null || who.IsDead() || Vector3.Distance(who.transform.position, transform.position) > radius) continue;
                float w = pair.Value.companion ? cw : 1f;
                if (w <= 0f) continue;
                if (pair.Value.companion) companions++; else players++;
                n += w;
                float pw = _power.TryGetValue(pair.Value.owner, out var pp) && Time.time - pp.at < 60f ? pp.power : 1f;
                powerSum += pw * w; weightSum += w;
            }
            foreach (var k in gone) _engaged.Remove(k);

            n = Mathf.Clamp(n, 1f, Mathf.Max(1, ScalingConfig.MaxPlayers.Value));
            _recent.Add((Time.time, n));
            float grace = ScalingConfig.ShrinkGrace.Value;
            _recent.RemoveAll(e => Time.time - e.at > grace);
            float held = n;
            foreach (var e in _recent) if (e.n > held) held = e.n;

            float groupPower = weightSum > 0f ? powerSum / weightSum : 1f;
            float pf = Mathf.Clamp(1f + ScalingConfig.PowerStrength.Value * (groupPower - 1f), ScalingConfig.PowerMin.Value, ScalingConfig.PowerMax.Value);
            float extra = held - 1f;
            float hp = (1f + ScalingConfig.HpPerPlayer.Value * extra) * pf;
            float res = Mathf.Max(ScalingConfig.ResistFloor.Value, 1f / (1f + ScalingConfig.ResistPerPlayer.Value * extra));
            float dmg = (1f + ScalingConfig.DamagePerPlayer.Value * extra) * (1f + ScalingConfig.PowerDamageShare.Value * (pf - 1f));
            Apply(zdo, held, n, groupPower, hp, dmg, res, players, companions);
        }


        private void Apply(ZDO zdo, float n, float now, float power, float hp, float dmg, float res, int players, int companions)
        {
            float oldHp = zdo.GetFloat(ZdoHp, 1f);
            float baseMax = zdo.GetFloat(ZdoBaseMax, 0f);
            float max = _body.GetMaxHealth();
            bool maxOk = baseMax > 0f && Mathf.Abs(max - baseMax * hp) < 0.5f;
            bool same = Mathf.Abs(zdo.GetFloat(ZdoN, 1f) - n) < 0.01f && Mathf.Abs(oldHp - hp) < 0.001f
                        && Mathf.Abs(zdo.GetFloat(ZdoDmg, 1f) - dmg) < 0.001f && Mathf.Abs(zdo.GetFloat(ZdoRes, 1f) - res) < 0.001f;
            if (maxOk && same) return;
            if (baseMax <= 0f) { baseMax = max / Mathf.Max(0.01f, oldHp); zdo.Set(ZdoBaseMax, baseMax); }
            else if (!maxOk && Mathf.Abs(max - baseMax) < 0.5f) { /* vanilla reset it (reload / SetLevel): scale from the base again */ }
            else if (!maxOk) { baseMax = max / Mathf.Max(0.01f, oldHp); zdo.Set(ZdoBaseMax, baseMax); } // something else changed the max: rebase

            float pct = _body.GetHealthPercentage();
            float newMax = baseMax * hp;
            _body.SetMaxHealth(newMax);
            _body.SetHealth(newMax * pct);
            zdo.Set(ZdoN, n); zdo.Set(ZdoHp, hp); zdo.Set(ZdoDmg, dmg); zdo.Set(ZdoRes, res); zdo.Set(ZdoPower, power);
            if (same) return; // only the max was restored
            MonsterLog.Info($"boss {Utils.GetPrefabName(gameObject)} scaled for {n:0.#} player{(Mathf.Approximately(n, 1f) ? "" : "s")} ({(n > now + 0.01f ? $"held from the last {ScalingConfig.ShrinkGrace.Value:0} s; now {now:0.#}: " : "")}{players} player(s), {companions} companion(s)), power {power:0.00}{(PowerReporter.BossHome.TryGetValue(Utils.GetPrefabName(gameObject), out var home) ? $" vs home {home}" : " vs local biome")}: "
                + $"HP x{hp:0.00} ({baseMax:0} -> {newMax:0}, {pct:P0} kept), dmg x{dmg:0.00}, resist x{res:0.00} {zdo.m_uid}");
        }

        /// <summary>"fmo_boss_scale [radius]": the live factors of every scaled boss / mini boss near the local player (tests, admins).</summary>
        public static void RegisterCommand()
        {
            new Terminal.ConsoleCommand("fmo_boss_scale", "[radius] - Fires Monster Overhaul: the group scaling of bosses and mini bosses nearby", args =>
            {
                var me = Player.m_localPlayer;
                if (me == null) { args.Context?.AddString("fmo_boss_scale: no local player"); return; }
                float r = args.Length > 1 && float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 60f;
                var list = new List<Character>();
                Character.GetCharactersInRange(me.transform.position, r, list);
                int shown = 0;
                foreach (var c in list)
                {
                    if (c == null || c.IsPlayer() || !IsScaled(c)) continue;
                    var z = c.GetComponent<ZNetView>().GetZDO();
                    string line = $"fmo_boss_scale: {Utils.GetPrefabName(c.gameObject)}={z.m_uid} n {z.GetFloat(ZdoN, 1f):0.#} power {z.GetFloat(ZdoPower, 1f):0.00} "
                        + $"HP x{z.GetFloat(ZdoHp, 1f):0.00} ({c.GetHealth():0}/{c.GetMaxHealth():0}, base {z.GetFloat(ZdoBaseMax, 0f):0}) dmg x{z.GetFloat(ZdoDmg, 1f):0.00} resist x{z.GetFloat(ZdoRes, 1f):0.00}";
                    args.Context?.AddString(line);
                    MonsterLog.Info(line);
                    shown++;
                }
                if (shown == 0) args.Context?.AddString($"fmo_boss_scale: no scaled boss or mini boss within {r:0} m");
            }, isCheat: true);
        }

        /// <summary>Damage dealt by a scaled creature (every peer: the ZDO).</summary>
        public static float DamageMultiplier(Character c)
        {
            if (!IsScaled(c)) return 1f;
            return c.GetComponent<ZNetView>().GetZDO().GetFloat(ZdoDmg, 1f);
        }

        internal static float Resist(Character c)
        {
            if (!IsScaled(c)) return 1f;
            return c.GetComponent<ZNetView>().GetZDO().GetFloat(ZdoRes, 1f);
        }
    }

    /// <summary>On the scaled creature's owner: who's in the fight, the group resist, and vanilla's own scaling cancelled (Replace Vanilla).</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    internal static class GroupScalerDamageTakenPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            if (__instance == null || hit == null || __instance.IsPlayer() || !ScalingConfig.Enabled.Value) return;
            var scaler = __instance.GetComponent<GroupScaler>();
            if (scaler == null) return;
            scaler.NoteEngaged(hit.GetAttacker());
            if (!GroupScaler.IsScaled(__instance)) return;
            float m = GroupScaler.Resist(__instance);
            // vanilla multiplies by GetDifficultyDamageScaleEnemy right after this prefix: divide it out
            if (ScalingConfig.ReplaceVanilla.Value && Game.instance != null)
            {
                float v = Game.instance.GetDifficultyDamageScaleEnemy(__instance.transform.position);
                if (v > 0f) m /= v;
            }
            if (!Mathf.Approximately(m, 1f)) hit.ApplyModifier(m);
        }
    }

    /// <summary>On the hit player's own client: vanilla's +4 %/player enemy damage cancelled for a scaled attacker (Replace Vanilla).</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
    internal static class GroupScalerDamageDealtPatch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            if (__instance == null || hit == null || !__instance.IsPlayer() || !ScalingConfig.Enabled.Value || !ScalingConfig.ReplaceVanilla.Value || Game.instance == null) return;
            var attacker = hit.GetAttacker();
            if (attacker == null || attacker.IsPlayer() || !GroupScaler.IsScaled(attacker)) return;
            float v = Game.instance.GetDifficultyDamageScalePlayer(__instance.transform.position);
            if (v > 0f && !Mathf.Approximately(v, 1f)) hit.ApplyModifier(1f / v);
        }
    }

    /// <summary>Every client, for its own player: send its power score to each scaled monster nearby (every 10 s).</summary>
    internal static class PowerReporter
    {
        // the vanilla bosses' home biomes (0.0.30; the power expectation follows the boss, not where it was summoned)
        internal static readonly System.Collections.Generic.Dictionary<string, Heightmap.Biome> BossHome = new System.Collections.Generic.Dictionary<string, Heightmap.Biome>
        {
            { "Eikthyr", Heightmap.Biome.Meadows }, { "gd_king", Heightmap.Biome.BlackForest }, { "Bonemass", Heightmap.Biome.Swamp },
            { "Dragon", Heightmap.Biome.Mountain }, { "GoblinKing", Heightmap.Biome.Plains }, { "SeekerQueen", Heightmap.Biome.Mistlands },
            { "Fader", Heightmap.Biome.AshLands },
        };
        private const float Every = 10f;
        private static float _next;
        private static readonly List<Character> Scan = new List<Character>();

        public static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + Every;
            var me = Player.m_localPlayer;
            if (me == null || me.IsDead() || !MonsterConfig.Enabled.Value || !ScalingConfig.Enabled.Value) return;
            Scan.Clear();
            Character.GetCharactersInRange(me.transform.position, ScalingConfig.EngageRadius.Value, Scan);
            foreach (var c in Scan)
            {
                if (c == null || c.IsPlayer() || !GroupScaler.IsScaled(c)) continue;
                var nv = c.GetComponent<ZNetView>();
                // a vanilla boss is rated against its HOME biome (a summoned Fader in the Meadows is still an Ashlands fight);
                // mini bosses (Elites, Champions, Hildir's) against where they stand
                var biome = BossHome.TryGetValue(Utils.GetPrefabName(c.gameObject), out var home) ? home : Heightmap.FindBiome(c.transform.position);
                var parts = PowerScore.For(me, biome);
                nv.InvokeRPC(GroupScaler.RpcPower, me.GetPlayerID(), parts.Total);
                MonsterLog.Debug($"power: {me.GetPlayerName()} -> {Utils.GetPrefabName(c.gameObject)} {parts.Total:0.00} vs {biome}{(BossHome.ContainsKey(Utils.GetPrefabName(c.gameObject)) ? " (home)" : "")} "
                    + $"(gear {parts.Gear:0.00}, quality {parts.Quality:0.00}, crafting {parts.Crafting:0.00}{(parts.HasFrc ? "" : " n/a")}, classes {parts.Classes:0.00}{(parts.HasClasses ? "" : " n/a")}, skills {parts.Skills:0.00})");
            }
        }
    }
}
