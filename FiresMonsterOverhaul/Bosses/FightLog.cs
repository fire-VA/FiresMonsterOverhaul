using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// The boss fight log (Fire 10-13, the boss-fight showcase; Tools\BOSS_FIGHT_SHOWCASE_PLAN.md). On the boss's OWNER, from
    /// its first damage taken:
    ///  - every 60 s an Info line "fight: &lt;boss&gt; t=MM:SS hp P% (cur/max) | dmg &lt;source&gt; X (Y%) ... | companions Z% |
    ///    scaled n N hp xH dmg xD | party &lt;name&gt; P% ..." - damage by source = each player by name, "companions" (any
    ///    CompanionController, every owner), "other" (adds, the environment);
    ///  - on its death "fight end: &lt;boss&gt; killed in MM:SS | dmg ... | party DPS D | effective max hp M (base B x H)".
    /// Damage is the boss's health actually lost per hit (after resistances, armour and FMO's scaling). An owner change
    /// mid-fight starts a new tally on the new owner (logged).
    /// </summary>
    internal sealed class FightLog : MonoBehaviour
    {
        private Character _boss;
        private ZNetView _nview;
        private float _engagedAt = -1f, _nextLine;
        private readonly Dictionary<string, float> _bySource = new Dictionary<string, float>();
        private float _total;

        private void Awake() { _boss = GetComponent<Character>(); _nview = GetComponent<ZNetView>(); }

        internal void Record(Character attacker, float lost)
        {
            if (lost <= 0f || _nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            if (_engagedAt < 0f) { _engagedAt = Time.time; _nextLine = Time.time + 60f; MonsterLog.Info($"fight: {Name} engaged {_nview.GetZDO().m_uid}"); }
            string src = SourceOf(attacker);
            _bySource.TryGetValue(src, out float v);
            _bySource[src] = v + lost;
            _total += lost;
        }

        private string Name => Utils.GetPrefabName(gameObject);

        private static string SourceOf(Character a)
        {
            if (a == null) return "other";
            if (a is Player p) return p.GetPlayerName();
            if (a.GetComponent<FiresCore.Npc.CompanionController>() != null) return "companions";
            return "other";
        }

        private void Update()
        {
            if (_engagedAt < 0f || Time.time < _nextLine || _boss == null || _boss.IsDead()) return;
            _nextLine = Time.time + 60f;
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            MonsterLog.Info($"fight: {Name} t={Clock(Time.time - _engagedAt)} hp {_boss.GetHealthPercentage() * 100f:0}% ({_boss.GetHealth():0}/{_boss.GetMaxHealth():0}) | {Sources()} | {Scaling()} | party {Party()}");
        }

        internal void OnDeath()
        {
            if (_engagedAt < 0f || _nview == null || !_nview.IsValid() || !_nview.IsOwner()) return;
            float secs = Mathf.Max(1f, Time.time - _engagedAt);
            var zdo = _nview.GetZDO();
            float hpX = zdo.GetFloat("fmo_grp_hp".GetStableHashCode(), 1f), baseMax = zdo.GetFloat("fmo_grp_basemax".GetStableHashCode(), 0f);
            MonsterLog.Info($"fight end: {Name} killed in {Clock(secs)} | {Sources()} | party DPS {_total / secs:0.0} | effective max hp {_boss.GetMaxHealth():0}{(baseMax > 0f ? $" (base {baseMax:0} x{hpX:0.00})" : "")} | {Scaling()} {zdo.m_uid}");
        }

        private string Sources()
        {
            if (_total <= 0f) return "dmg none";
            var parts = _bySource.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value:0} ({kv.Value / _total:P0})");
            _bySource.TryGetValue("companions", out float comp);
            return $"dmg {string.Join(", ", parts)} | companions {comp / _total:P0}";
        }

        private string Scaling()
        {
            var zdo = _nview.GetZDO();
            return $"scaled n {zdo.GetFloat("fmo_grp_n".GetStableHashCode(), 1f):0.#} hp x{zdo.GetFloat("fmo_grp_hp".GetStableHashCode(), 1f):0.00} dmg x{zdo.GetFloat("fmo_grp_dmg".GetStableHashCode(), 1f):0.00}";
        }

        private string Party()
        {
            var near = Player.GetAllPlayers().Where(p => p != null && Vector3.Distance(p.transform.position, transform.position) < 60f).ToList();
            return near.Count == 0 ? "none near" : string.Join(", ", near.Select(p => $"{p.GetPlayerName()} {(p.IsDead() ? "DEAD" : (p.GetHealthPercentage() * 100f).ToString("0") + "%")}"));
        }

        private static string Clock(float s) => $"{(int)(s / 60f):00}:{(int)(s % 60f):00}";
    }

    /// <summary>Health actually lost per hit on a logged boss, by attacker (owner side).</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    internal static class FightLogDamagePatch
    {
        private static void Prefix(Character __instance, out float __state) => __state = __instance != null && __instance.IsBoss() ? __instance.GetHealth() : -1f;

        private static void Postfix(Character __instance, HitData hit, float __state)
        {
            if (__state < 0f || __instance == null) return;
            var log = __instance.GetComponent<FightLog>();
            if (log == null) return;
            log.Record(hit?.GetAttacker(), __state - __instance.GetHealth());
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
    internal static class FightLogDeathPatch
    {
        private static void Prefix(Character __instance) => __instance?.GetComponent<FightLog>()?.OnDeath();
    }
}
