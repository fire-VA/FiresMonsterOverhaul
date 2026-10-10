using System.Collections.Generic;
using FiresMonsterOverhaul.Abilities;
using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>Doc 22's four healing patterns, each with a counter. Failing one lengthens the fight; it never wipes the group.</summary>
    public enum HealPattern
    {
        None = 0,
        Tethered = 1,   // objects around the arena feed it health; it's immune while any stands. Counter: destroy them
        Channelled = 2, // a long interruptible heal cast, repeated. Counter: interrupt every cast
        Feeding = 3,    // adds walk to it and heal it when they arrive. Counter: kill or slow them on the way
        Shelled = 4,    // an absorb shield on a timer; if it isn't broken in time, the boss heals. Counter: burst it
    }

    /// <summary>
    /// The healing patterns (34-PHASE4-BOSSES-PLAN.md "the parts"), on the boss's owner, one at a time. Their state lives on
    /// the boss's ZDO (the pattern, its start and end, the shield, the objects' ZDOIDs), so an owner change carries on with
    /// it, and every viewer's nameplate shows it (ZdoPatternInfo).
    /// </summary>
    public sealed partial class BossDirector
    {
        public static readonly int ZdoPattern = "fmo_boss_pattern".GetStableHashCode();
        public static readonly int ZdoPatternStart = "fmo_boss_pattern_start".GetStableHashCode();
        public static readonly int ZdoPatternUntil = "fmo_boss_pattern_until".GetStableHashCode();
        public static readonly int ZdoPatternShield = "fmo_boss_pattern_shield".GetStableHashCode();
        public static readonly int ZdoPatternCount = "fmo_boss_pattern_n".GetStableHashCode();
        public static readonly int ZdoPatternInfo = "fmo_boss_pattern_info".GetStableHashCode();
        private const string ZdoPatternObj = "fmo_boss_pattern_obj_"; // + index: a ZDOID (string keys)
        public const string ZdoPropOf = "fmo_prop_of";                // on a pattern object or roots: its boss
        public const string ZdoFeederOf = "fmo_feeder_of";            // on a Feeding add: its boss
        private const float MinPropHealth = 50f;
        private const float ChannelRingRadius = 4f;

        private int _objectsAlive;   // Tethered: saplings standing, for the immunity check (owner)
        private float _patternHealed;
        private int _channelCasts, _channelHeals;
        private float _nextChannel;
        private string _info = "";

        internal HealPattern Pattern => Nview != null && Nview.IsValid() ? (HealPattern)Zdo.GetInt(ZdoPattern) : HealPattern.None;

        public static string PatternInfo(ZDO zdo) => zdo != null ? zdo.GetString(ZdoPatternInfo) : "";

        private long Now => ZNet.instance.GetTime().Ticks;
        private float SecondsSince(long ticks) => (Now - ticks) / (float)TicksPerSecond;

        /// <summary>Starts a pattern (a boss script's phase, or the fmo_boss_pattern cheat). One at a time.</summary>
        internal bool StartPattern(HealPattern pattern, string why, string feederPrefab = null)
        {
            if (pattern == HealPattern.None || Pattern != HealPattern.None || Body == null || Body.IsDead()) return false;
            var zdo = Zdo;
            zdo.Set(ZdoPattern, (int)pattern);
            zdo.Set(ZdoPatternStart, Now);
            _patternHealed = 0f;
            _channelCasts = 0;
            _channelHeals = 0;
            _objectsAlive = 0;
            float max = Body.GetMaxHealth();
            switch (pattern)
            {
                case HealPattern.Tethered:
                    {
                        var ids = PlaceAround(BossProps.Heartwood, MonsterConfig.TetherCount.Value, MonsterConfig.TetherRadius.Value,
                            Mathf.Max(MinPropHealth, max * MonsterConfig.TetherHealth.Value));
                        _objectsAlive = ids.Count;
                        MonsterLog.Debug($"boss: {Prefab} healing pattern tethered ({why}): {ids.Count} Heartwood at {MonsterConfig.TetherRadius.Value:0} m "
                            + $"({Mathf.Max(MinPropHealth, max * MonsterConfig.TetherHealth.Value):0} HP each; immune, heals {MonsterConfig.TetherHealPerSecond.Value:P0}/s while any stands) {Ids(ids)} {zdo.m_uid}");
                        if (ids.Count == 0) EndPattern("no Heartwood could be placed", false);
                        break;
                    }
                case HealPattern.Channelled:
                    zdo.Set(ZdoPatternUntil, Now + (long)(MonsterConfig.ChannelPhaseSeconds.Value * TicksPerSecond));
                    _nextChannel = 0f;
                    MonsterLog.Debug($"boss: {Prefab} healing pattern channelled ({why}): a {MonsterConfig.ChannelCastSeconds.Value:0.#} s interruptible heal "
                        + $"({MonsterConfig.ChannelHeal.Value:P0} of max) every {MonsterConfig.ChannelEvery.Value:0} s for {MonsterConfig.ChannelPhaseSeconds.Value:0} s {zdo.m_uid}");
                    break;
                case HealPattern.Shelled:
                    {
                        float shield = max * MonsterConfig.ShellShare.Value;
                        zdo.Set(ZdoPatternShield, shield);
                        zdo.Set(ZdoPatternUntil, Now + (long)(MonsterConfig.ShellSeconds.Value * TicksPerSecond));
                        MonsterLog.Debug($"boss: {Prefab} healing pattern shelled ({why}): a {shield:0} shield for {MonsterConfig.ShellSeconds.Value:0} s; "
                            + $"if it holds, heals {MonsterConfig.ShellHeal.Value:P0} of max {zdo.m_uid}");
                        break;
                    }
                case HealPattern.Feeding:
                    {
                        string prefab = string.IsNullOrEmpty(feederPrefab) ? MonsterConfig.FeederPrefab.Value : feederPrefab;
                        var ids = SendFeeders(prefab);
                        MonsterLog.Debug($"boss: {Prefab} healing pattern feeding ({why}): {ids.Count} {prefab} from {MonsterConfig.FeederDistance.Value:0} m "
                            + $"(each heals {MonsterConfig.FeederHeal.Value:P0} of max on arrival) {Ids(ids)} {zdo.m_uid}");
                        if (ids.Count == 0) EndPattern($"no {prefab} could be sent", false);
                        break;
                    }
            }
            UpdateInfo();
            return true;
        }

        private void TickPattern()
        {
            var pattern = Pattern;
            if (pattern == HealPattern.None) return;
            var zdo = Zdo;
            float max = Body.GetMaxHealth();
            float seconds = SecondsSince(zdo.GetLong(ZdoPatternStart, Now));
            switch (pattern)
            {
                case HealPattern.Tethered:
                    {
                        int alive = AliveObjects();
                        if (alive < _objectsAlive) MonsterLog.Debug($"boss: {Prefab} tether broken ({alive} left) {zdo.m_uid}");
                        _objectsAlive = alive;
                        if (alive == 0) { EndPattern($"tethers all broken after {seconds:0} s (healed {_patternHealed:0})", false); return; }
                        HealBy(max * MonsterConfig.TetherHealPerSecond.Value * Tick, false);
                        break;
                    }
                case HealPattern.Channelled:
                    {
                        var cast = Cast();
                        bool over = Now >= zdo.GetLong(ZdoPatternUntil);
                        if (over && !cast.Active)
                        {
                            EndPattern($"channelled phase over after {seconds:0} s: {_channelCasts} cast(s), {_channelHeals} healed, "
                                + $"{_channelCasts - _channelHeals} interrupted (healed {_patternHealed:0})", false);
                            return;
                        }
                        if (!over && !cast.Active && Time.time >= _nextChannel) BeginChannel(max);
                        break;
                    }
                case HealPattern.Shelled:
                    {
                        if (zdo.GetFloat(ZdoPatternShield) <= 0f) { EndPattern($"shell broken after {seconds:0.0} s", false); return; }
                        if (Now < zdo.GetLong(ZdoPatternUntil)) break;
                        HealBy(max * MonsterConfig.ShellHeal.Value, true);
                        EndPattern($"shell held {seconds:0} s: healed {_patternHealed:0} ({MonsterConfig.ShellHeal.Value:P0} of max)", false);
                        return;
                    }
                case HealPattern.Feeding:
                    {
                        int alive = AliveObjects();
                        _objectsAlive = alive;
                        if (alive == 0) { EndPattern($"feeding over after {seconds:0} s", false); return; }
                        break;
                    }
            }
            UpdateInfo();
        }

        private void BeginChannel(float max)
        {
            float heal = MonsterConfig.ChannelHeal.Value;
            float secs = MonsterConfig.ChannelCastSeconds.Value;
            if (!Cast().Begin("channel heal", secs, true, 1, ChannelRingRadius, () =>
            {
                _channelHeals++;
                float healed = HealBy(Body.GetMaxHealth() * heal, true);
                MonsterLog.Debug($"boss: {Prefab} channel heal healed {healed:0} ({heal:P0} of max) {Zdo.m_uid}");
            }, holdStill: true)) return;
            _channelCasts++;
            _nextChannel = Time.time + MonsterConfig.ChannelEvery.Value;
            MonsterLog.Debug($"boss: {Prefab} channel heal cast ({secs:0.0} s, interruptible; heals {heal:P0} of max) {Zdo.m_uid}");
        }

        /// <param name="removeObjects">True when the boss died: its saplings and feeders go with it.</param>
        internal void EndPattern(string how, bool removeObjects)
        {
            var zdo = Zdo;
            var pattern = Pattern;
            int removed = 0;
            if (removeObjects)
            {
                int n = zdo.GetInt(ZdoPatternCount);
                for (int i = 0; i < n; i++)
                {
                    var id = zdo.GetZDOID(ZdoPatternObj + i);
                    if (!BossProps.Exists(id)) continue;
                    BossProps.Remove(id);
                    removed++;
                }
            }
            zdo.Set(ZdoPattern, (int)HealPattern.None);
            zdo.Set(ZdoPatternCount, 0);
            zdo.Set(ZdoPatternShield, 0f);
            _objectsAlive = 0;
            _info = "";
            zdo.Set(ZdoPatternInfo, "");
            MonsterLog.Debug($"boss: {Prefab} {pattern.ToString().ToLowerInvariant()} ended: {how}{(removed > 0 ? $"; {removed} object(s) removed" : "")} {zdo.m_uid}");
        }

        private float HealBy(float amount, bool showText)
        {
            float missing = Body.GetMaxHealth() - Body.GetHealth();
            if (amount <= 0f || missing <= 0f) return 0f;
            float healed = Mathf.Min(amount, missing);
            Body.Heal(healed, showText);
            _patternHealed += healed;
            return healed;
        }

        private void UpdateInfo()
        {
            var zdo = Zdo;
            string info;
            switch (Pattern)
            {
                case HealPattern.Tethered: info = $"Tethered · {_objectsAlive} Heartwood (immune)"; break;
                case HealPattern.Channelled: info = "Channelling heals"; break;
                case HealPattern.Shelled: info = $"Shelled {zdo.GetFloat(ZdoPatternShield):0}"; break;
                case HealPattern.Feeding: info = $"Feeding · {_objectsAlive} coming"; break;
                default: info = ""; break;
            }
            if (info == _info) return;
            _info = info;
            zdo.Set(ZdoPatternInfo, info);
        }

        // Objects (saplings, feeders) evenly round the boss, their ZDOIDs on its ZDO.
        private List<ZDOID> PlaceAround(string prefab, int count, float radius, float health)
        {
            var ids = new List<ZDOID>();
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float angle = (start + i * 360f / Mathf.Max(1, count)) * Mathf.Deg2Rad;
                Vector3 at = transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                var nv = BossProps.Place(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), health, Zdo.m_uid);
                if (nv != null) ids.Add(nv.GetZDO().m_uid);
            }
            SetObjects(ids);
            return ids;
        }

        private List<ZDOID> SendFeeders(string prefab)
        {
            var ids = new List<ZDOID>();
            int count = Adds.CountFor(transform.position, MonsterConfig.FeederCount.Value, MonsterConfig.FeederCount.Value + 3);
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float angle = (start + i * 360f / Mathf.Max(1, count)) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 at = transform.position + dir * MonsterConfig.FeederDistance.Value;
                var feeder = Adds.SpawnCopy(Body, prefab, at, 1, Quaternion.LookRotation(-dir), (c, z) =>
                {
                    z.Set(ZdoFeederOf, Zdo.m_uid);
                    z.Set(Affixes.AffixRunner.ZdoSummonedBy, Zdo.m_uid);
                    if (c.GetComponent<Feeder>() == null) c.gameObject.AddComponent<Feeder>();
                });
                if (feeder != null) ids.Add(feeder.GetZDOID());
            }
            SetObjects(ids);
            return ids;
        }

        private void SetObjects(List<ZDOID> ids)
        {
            var zdo = Zdo;
            zdo.Set(ZdoPatternCount, ids.Count);
            for (int i = 0; i < ids.Count; i++) zdo.Set(ZdoPatternObj + i, ids[i]);
        }

        private int AliveObjects()
        {
            var zdo = Zdo;
            int n = zdo.GetInt(ZdoPatternCount), alive = 0;
            for (int i = 0; i < n; i++) if (BossProps.Exists(zdo.GetZDOID(ZdoPatternObj + i))) alive++;
            return alive;
        }

        private static string Ids(List<ZDOID> ids) => ids.Count == 0 ? "[]" : "[" + string.Join(", ", ids) + "]";

        // ---------------------------------------------------------------- damage hooks (the ApplyDamage prefix, owner)

        /// <summary>Tethered: immune while any Heartwood stands.</summary>
        public static bool ImmuneNow(Character c)
        {
            if (c == null || c.IsPlayer() || !c.IsBoss() || !MonsterConfig.Bosses.Value) return false;
            var director = c.GetComponent<BossDirector>();
            return director != null && director.Nview != null && director.Nview.IsValid() && director.Nview.IsOwner()
                && director._objectsAlive > 0 && director.Pattern == HealPattern.Tethered;
        }

        /// <summary>Shelled: the shell takes the hit first. Returns what gets through.</summary>
        public static float ShellAbsorb(Character c, float incoming)
        {
            if (c == null || c.IsPlayer() || incoming <= 0f || !c.IsBoss() || !MonsterConfig.Bosses.Value) return incoming;
            var director = c.GetComponent<BossDirector>();
            if (director == null || director.Nview == null || !director.Nview.IsValid() || !director.Nview.IsOwner()) return incoming;
            if (director.Pattern != HealPattern.Shelled) return incoming;
            var zdo = director.Zdo;
            float shell = zdo.GetFloat(ZdoPatternShield);
            if (shell <= 0f) return incoming;
            float absorbed = Mathf.Min(shell, incoming);
            float left = shell - absorbed;
            zdo.Set(ZdoPatternShield, left);
            MonsterLog.Debug($"boss: {director.Prefab} shell absorbed {absorbed:0.0} of {incoming:0.0} ({left:0.0} left) {zdo.m_uid}");
            return incoming - absorbed;
        }
    }

    /// <summary>
    /// A Feeding add: it walks to its boss (vanilla MoveTo, from the MonsterAI.UpdateAI prefix) and heals it on arrival,
    /// then is gone. Runs on the add's owner; it fights normally once its boss is gone.
    /// </summary>
    internal sealed class Feeder : MonoBehaviour
    {
        private const float ArriveMargin = 1.5f;
        private ZNetView _nview;
        private Character _body;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _body = GetComponent<Character>();
        }

        public bool OverrideAI(MonsterAI ai, float dt)
        {
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _body == null || _body.IsDead()) return false;
            var bossId = _nview.GetZDO().GetZDOID(BossDirector.ZdoFeederOf);
            var bossGo = bossId != ZDOID.None && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(bossId) : null;
            var boss = bossGo != null ? bossGo.GetComponent<Character>() : null;
            if (boss == null || boss.IsDead()) return false;
            Vector3 to = boss.transform.position;
            Vector3 d = to - transform.position;
            if (new Vector2(d.x, d.z).magnitude > boss.GetRadius() + _body.GetRadius() + ArriveMargin)
            {
                ai.MoveTo(dt, to, 0.5f, true);
                return true;
            }
            float max = boss.GetMaxHealth();
            float heal = Mathf.Min(max * MonsterConfig.FeederHeal.Value, Mathf.Max(0f, max - boss.GetHealth()));
            if (heal > 0f) boss.Heal(heal, true);
            MonsterLog.Debug($"boss: {Utils.GetPrefabName(boss.gameObject)} fed by {Utils.GetPrefabName(gameObject)} (+{heal:0}, {MonsterConfig.FeederHeal.Value:P0} of max) {_nview.GetZDO().m_uid}");
            ZNetScene.instance.Destroy(gameObject);
            return true;
        }
    }
}
