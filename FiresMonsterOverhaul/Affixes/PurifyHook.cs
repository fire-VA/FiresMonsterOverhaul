using System.Collections.Generic;
using System.Reflection;

namespace FiresMonsterOverhaul.Affixes
{
    /// <summary>
    /// Core 0.2.328+ ([generator] 10-08): PurifyEffect.RegisterDebuff(name) puts an FMO debuff on Purify's cleanse list.
    /// Looked up by reflection so FMO still loads (with uncleansable debuffs) on an older Core.
    /// </summary>
    internal static class PurifyHook
    {
        private static readonly HashSet<string> Done = new HashSet<string>();
        private static MethodInfo _register;
        private static bool _looked;

        public static void Register(string effectName)
        {
            if (string.IsNullOrEmpty(effectName) || Done.Contains(effectName)) return;
            if (!_looked)
            {
                _looked = true;
                _register = HarmonyLib.AccessTools.Method("FiresCore.Npc.Archetypes.StatusEffects.Healer.PurifyEffect:RegisterDebuff", new[] { typeof(string) });
            }
            if (_register == null)
            {
                MonsterLog.Debug($"purify: this Core has no PurifyEffect.RegisterDebuff (needs Core 0.2.328+): Purify can't cleanse {effectName}");
                Done.Add(effectName);
                return;
            }
            _register.Invoke(null, new object[] { effectName });
            Done.Add(effectName);
        }
    }
}
