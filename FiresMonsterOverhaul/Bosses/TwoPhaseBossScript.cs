using System.Collections.Generic;
using BepInEx.Configuration;
using FiresMonsterOverhaul.Abilities;
using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// 0.0.40 (Fire: phases for Moder, Yagluth, The Queen and Fader): the shared shape of the four later bosses' scripts, on
    /// the same BossDirector as Eikthyr / the Elder / Bonemass (owner-side 0.5 s tick, phases fired once through FirePhase's
    /// ZDO bitmask and its "phase" log line, the soft enrage and group scaling untouched). Two phases like Bonemass's
    /// ("two thirds" and "one third"), their health shares and an on/off flag in [10 - Bosses].
    /// </summary>
    internal abstract class TwoPhaseBossScript : BossScript
    {
        internal const int PhaseFirst = 1 << 0;
        internal const int PhaseSecond = 1 << 1;
        internal const string FirstName = "two thirds";
        internal const string SecondName = "one third";
        private const float AddOffset = 5f;

        protected abstract ConfigEntry<bool> Enabled { get; }
        internal abstract ConfigEntry<float> FirstAt { get; }
        internal abstract ConfigEntry<float> SecondAt { get; }
        protected abstract void OnFirstPhase();
        protected abstract void OnSecondPhase();
        protected virtual void TickPhases(bool inCombat) { }

        /// <summary>True once the second phase has fired (its abilities then repeat).</summary>
        protected bool InSecond => (D.Zdo.GetInt(BossDirector.ZdoPhases) & PhaseSecond) != 0;

        public sealed override void Tick(bool inCombat, float healthFraction)
        {
            if (!Enabled.Value) return;
            if (healthFraction <= FirstAt.Value && D.FirePhase(PhaseFirst, FirstName, healthFraction)) OnFirstPhase();
            if (healthFraction <= SecondAt.Value && D.FirePhase(PhaseSecond, SecondName, healthFraction)) OnSecondPhase();
            TickPhases(inCombat);
        }

        /// <summary>Plain adds round the boss (Adds.SpawnCopy: no tier, no drops, the boss's multipliers, on its target), like
        /// Eikthyr's spirit stags without the spirit form. Each is marked as the boss's (AffixRunner.ZdoSummonedBy).</summary>
        protected List<ZDOID> SpawnAdds(string prefab, int baseCount, int max, string what)
        {
            int count = Adds.CountFor(D.transform.position, baseCount, max);
            var ids = new List<ZDOID>();
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float angle = (start + i * 360f / Mathf.Max(1, count)) * Mathf.Deg2Rad;
                Vector3 at = D.transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * AddOffset;
                var add = Adds.SpawnCopy(D.Body, prefab, at, 1, D.transform.rotation, (c, z) => z.Set(Affixes.AffixRunner.ZdoSummonedBy, D.Zdo.m_uid));
                if (add != null) ids.Add(add.GetZDOID());
            }
            MonsterLog.Debug($"boss: {D.Prefab} adds: {ids.Count} {prefab} {what} [{string.Join(", ", ids)}] {D.Zdo.m_uid}");
            return ids;
        }
    }
}
