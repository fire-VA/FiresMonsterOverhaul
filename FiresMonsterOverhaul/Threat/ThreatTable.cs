using System.Collections.Generic;
using UnityEngine;

namespace FiresMonsterOverhaul.Threat
{
    /// <summary>
    /// Owner-side threat per attacker (doc 21): damage builds threat, healing someone the monster fights builds
    /// half as much, and the table is forgotten after a spell out of combat. The brain reads Top / Of to switch
    /// targets with a margin, so the monster doesn't bounce between players.
    /// </summary>
    public sealed class ThreatTable : MonoBehaviour
    {
        // ZDO.Set/GetZDOID take a string key (a ZDOID is stored as two longs under derived keys).
        public const string ZdoTarget = "fmo_target";

        private readonly Dictionary<ZDOID, float> _threat = new Dictionary<ZDOID, float>();
        private float _lastActivity = -999f;

        public int Count => _threat.Count;

        /// <summary>Whether this source has any threat on the table (set drops: "engaged").</summary>
        public bool Has(ZDOID id) => _threat.ContainsKey(id);

        public void Add(Character source, float amount, string why)
        {
            if (source == null || amount <= 0f) return;
            ZDOID id = source.GetZDOID();
            if (id.IsNone()) return;
            _threat.TryGetValue(id, out float current);
            _threat[id] = current + amount;
            _lastActivity = Time.time;
            if (MonsterLog.DebugEnabled)
                MonsterLog.Debug($"threat: {Utils.GetPrefabName(gameObject)} +{amount:0.#} from {source.GetHoverName()} ({why}) = {_threat[id]:0.#}");
        }

        public void AddById(ZDOID id, float amount)
        {
            if (id.IsNone() || amount <= 0f) return;
            _threat.TryGetValue(id, out float current);
            _threat[id] = current + amount;
            _lastActivity = Time.time;
        }

        public float Of(Character c)
        {
            if (c == null) return 0f;
            return _threat.TryGetValue(c.GetZDOID(), out float v) ? v : 0f;
        }

        /// <summary>The highest-threat living character in range, or null.</summary>
        public Character Top(Vector3 from, float range, out float threat)
        {
            threat = 0f;
            Character best = null;
            foreach (var pair in _threat)
            {
                var obj = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(pair.Key) : null;
                var c = obj != null ? obj.GetComponent<Character>() : null;
                if (c == null || c.IsDead()) continue;
                if ((c.transform.position - from).sqrMagnitude > range * range) continue;
                if (pair.Value > threat) { threat = pair.Value; best = c; }
            }
            return best;
        }

        public void Forget(string why)
        {
            if (_threat.Count == 0) return;
            MonsterLog.Debug($"threat: {Utils.GetPrefabName(gameObject)} table cleared ({why})");
            _threat.Clear();
        }

        public void DecayIfIdle(bool inCombat, float decaySeconds)
        {
            if (inCombat) { _lastActivity = Time.time; return; }
            if (_threat.Count > 0 && Time.time - _lastActivity > decaySeconds) Forget("out of combat");
        }
    }
}
