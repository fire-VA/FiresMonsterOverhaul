using System.Collections.Generic;
using FiresCore.Npc.Archetypes;
using FiresMonsterOverhaul.Agent;

namespace FiresMonsterOverhaul.Threat
{
    /// <summary>
    /// Doc 21: healing someone a monster is fighting builds threat at Heal Threat Factor of damage. Core raises
    /// AbilityHeals.OnHealApplied on the peer that owns the HEALED character, which is often not the monster's
    /// owner, so the threat goes to each monster targeting the healed character through its FMO_Threat RPC.
    /// </summary>
    internal static class HealThreat
    {
        private static readonly List<Character> Scan = new List<Character>();
        private static bool _subscribed;

        public static void Subscribe()
        {
            if (_subscribed) return;
            AbilityHeals.OnHealApplied += OnHeal;
            _subscribed = true;
        }

        public static void Unsubscribe()
        {
            if (!_subscribed) return;
            AbilityHeals.OnHealApplied -= OnHeal;
            _subscribed = false;
        }

        private static void OnHeal(Character healer, Character healed, float amount)
        {
            if (healer == null || healed == null || amount <= 0f || !MonsterConfig.Enabled.Value) return;
            ZDOID healedId = healed.GetZDOID();
            ZDOID healerId = healer.GetZDOID();
            if (healedId.IsNone() || healerId.IsNone()) return;

            Scan.Clear();
            Character.GetCharactersInRange(healed.transform.position, 40f, Scan);
            foreach (var c in Scan)
            {
                if (c == null || c.IsDead() || c.GetComponent<MonsterBrain>() == null) continue;
                var nv = c.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid()) continue;
                if (nv.GetZDO().GetZDOID(ThreatTable.ZdoTarget) != healedId) continue;
                nv.InvokeRPC(MonsterBrain.RpcThreat, healerId, amount * MonsterConfig.HealThreatFactor.Value);
            }
        }
    }
}
