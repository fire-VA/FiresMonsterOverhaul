using System;
using System.Reflection;
using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// A boss's spirit add. Fire 10-08 (via the lead): Eikthyr's 50 % adds are "the small eik copies using the deep north
    /// boss summon material". A spirit is a copy of its boss, small, in the Frozen King fight's aspect material (Core's
    /// Variants "crystal" = vanilla Aspect_mat, [classes-ui]), with a share of the boss's health and none of its boss
    /// flags: no boss bar, no defeat key, no dream, no boss music, no director, no drops.
    /// The ZDO flag rides to every peer and each peer makes its own instance look and act the part (the MonsterAI.Awake
    /// patch on a peer that loads it; Make on the peer that spawns it).
    /// </summary>
    internal static class SpiritForm
    {
        public static readonly int ZdoSpirit = "fmo_spirit".GetStableHashCode();
        public static readonly int ZdoSpiritName = "fmo_spirit_name".GetStableHashCode();
        public static readonly int ZdoSpiritScale = "fmo_spirit_scale".GetStableHashCode();
        private const string VariantId = "crystal";
        private const float LookRetrySeconds = 2f;

        public static bool IsSpirit(ZDO zdo) => zdo != null && zdo.GetBool(ZdoSpirit);

        /// <summary>On the spawning peer, from Adds.SpawnCopy's prepare step (before its level, health and alert).</summary>
        public static void Make(Character c, ZDO z, string name, float healthShare, float scale)
        {
            z.Set(ZdoSpirit, true);
            z.Set(ZdoSpiritName, name);
            z.Set(ZdoSpiritScale, scale);
            z.Set(Setup.CreatureSetup.ZdoHealthMult, z.GetFloat(Setup.CreatureSetup.ZdoHealthMult, 1f) * Mathf.Clamp(healthShare, 0.01f, 1f));
            Apply(c);
        }

        /// <summary>Every peer: strips the boss flags and puts on the spirit look.</summary>
        public static void Apply(Character c)
        {
            if (c == null) return;
            var nv = c.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            var zdo = nv.GetZDO();
            if (!IsSpirit(zdo)) return;
            c.m_boss = false;
            c.m_defeatSetGlobalKey = "";
            c.m_dreamCinematic = "";
            c.m_bossEvent = "";
            string name = zdo.GetString(ZdoSpiritName);
            if (!string.IsNullOrEmpty(name)) c.m_name = name;
            var ai = c.GetComponent<BaseAI>();
            if (ai != null) ai.m_alertedMessage = "";
            var director = c.GetComponent<BossDirector>();
            if (director != null) { director.enabled = false; UnityEngine.Object.Destroy(director); }
            c.GetComponent<CharacterDrop>()?.SetDropsEnabled(false);
            c.transform.localScale = Vector3.one * Mathf.Clamp(zdo.GetFloat(ZdoSpiritScale, 1f), 0.1f, 2f);
            ApplyLook(c.gameObject);
        }

        // Core 0.2.328+ (FiresCore.Variants, [classes-ui]): VariantApply.Apply(root, id, isBody). Looked up by reflection
        // so FMO still loads on an older Core (then a spirit keeps its boss's own look).
        private static MethodInfo _variantApply;
        private static bool _looked;

        // Applying it also tints the spirit's own FX (Eikthyr's lightning) crystal cyan on that peer ([classes-ui] 10-08).
        private static void ApplyLook(GameObject go)
        {
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return; // nothing is drawn on a dedicated server
            if (!_looked)
            {
                _looked = true;
                _variantApply = HarmonyLib.AccessTools.Method("FiresCore.Variants.VariantApply:Apply", new[] { typeof(GameObject), typeof(string), typeof(Func<Renderer, bool>) });
                if (_variantApply == null) MonsterLog.Debug("spirit: this Core has no Variants (needs Core 0.2.328+): spirits keep their boss's look");
            }
            if (_variantApply == null || TryLook(go)) return;
            // Aspect_mat is found among loaded materials; if it isn't loaded yet, one more try a moment later ([classes-ui]).
            if (Plugin.Instance != null) Plugin.Instance.StartCoroutine(RetryLook(go));
        }

        private static System.Collections.IEnumerator RetryLook(GameObject go)
        {
            yield return new WaitForSeconds(LookRetrySeconds);
            if (go != null && !TryLook(go)) MonsterLog.Debug($"spirit: {Utils.GetPrefabName(go)} the '{VariantId}' look didn't apply after a retry (Aspect_mat not loaded?): it keeps its boss's look");
        }

        private static bool TryLook(GameObject go)
        {
            try
            {
                return _variantApply.Invoke(null, new object[] { go, VariantId, null }) is bool b && b;
            }
            catch (Exception e)
            {
                MonsterLog.Debug($"spirit: {Utils.GetPrefabName(go)} the '{VariantId}' look failed: {e.InnerException?.Message ?? e.Message}");
                return true; // no retry for a throw
            }
        }
    }
}
