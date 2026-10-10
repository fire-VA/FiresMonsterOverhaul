using System.Collections.Generic;
using System.Globalization;
using FiresMonsterOverhaul.Affixes;
using FiresMonsterOverhaul.Agent;
using UnityEngine;

namespace FiresMonsterOverhaul.Casts
{
    /// <summary>
    /// The test hook for a deterministic fresh cast (Round 7, 10-07: the Shaman healed during the drill's hurt step and
    /// vanilla's heal cooldown then held the next one ~40 s). "fmo_cast_ready [radius]" makes every monster this peer
    /// owns within the radius ready to cast again: the heal weapon's vanilla cooldown, the war cry, Mending Aura's pulse,
    /// Pack Leader's inspire and the Veteran abilities (Ground Blast, Charge, Sidestep). Ownership matters: casts run on the creature's owner, and fmo_tier spawns are owned
    /// by the peer that typed it.
    /// </summary>
    internal static class CastReadyCommand
    {
        private const float DefaultRadius = 30f;

        public static void Register()
        {
            new Terminal.ConsoleCommand("fmo_cast_ready", "[radius] - Fires Monster Overhaul: reset cast cooldowns on monsters this peer owns nearby, for tests", args =>
            {
                float radius = DefaultRadius;
                if (args.Length >= 2 && !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out radius))
                {
                    args.Context?.AddString($"fmo_cast_ready: '{args[1]}' is not a radius");
                    return;
                }
                var player = Player.m_localPlayer;
                if (player == null) { args.Context?.AddString("fmo_cast_ready: no local player"); return; }

                var near = new List<Character>();
                Character.GetCharactersInRange(player.transform.position, radius, near);
                int reset = 0, notOwned = 0;
                foreach (var c in near)
                {
                    if (c == null || c.IsPlayer() || c.IsDead() || c.GetComponent<MonsterAI>() == null) continue;
                    var nv = c.GetComponent<ZNetView>();
                    if (nv == null || !nv.IsValid()) continue;
                    if (!nv.IsOwner()) { notOwned++; continue; }
                    if (c is Humanoid humanoid && humanoid.GetInventory() != null)
                    {
                        foreach (var item in humanoid.GetInventory().GetAllItems())
                        {
                            var target = item.m_shared.m_aiTargetType;
                            if (target == ItemDrop.ItemData.AiTarget.FriendHurt || target == ItemDrop.ItemData.AiTarget.Friend) item.m_lastAttackTime = 0f;
                        }
                    }
                    c.GetComponent<MonsterBrain>()?.ResetCooldowns();
                    c.GetComponent<AffixRunner>()?.ResetCooldowns();
                    c.GetComponent<Abilities.VeteranAbilities>()?.ResetCooldowns();
                    c.GetComponent<Bosses.BossDirector>()?.ResetCooldowns();
                    reset++;
                }
                string line = $"fmo_cast_ready: reset {reset} creature(s) within {radius:0.#} m ({notOwned} not owned here)";
                MonsterLog.Debug(line);
                args.Context?.AddString(line);
            }, isCheat: true);
        }
    }
}
