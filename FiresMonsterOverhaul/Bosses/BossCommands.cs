using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// The test hook for the healing patterns (34-PHASE4 slice 2: prove all four before the bosses that use them):
    /// "fmo_boss_pattern &lt;tethered|channelled|feeding|shelled|end&gt; [feeder prefab] [radius]" starts one on the nearest
    /// boss this peer owns (casts and phases run on the boss's owner, and a spawned boss is owned by the peer that typed it).
    /// </summary>
    internal static class BossCommands
    {
        private const float DefaultRadius = 40f;

        public static void Register()
        {
            BossPhaseTest.Register();   // 0.0.40: fmo_boss_phase_test (Moder, Yagluth, The Queen, Fader phases)
            new Terminal.ConsoleCommand("fmo_boss_pattern", "<tethered|channelled|feeding|shelled|end> [feeder prefab] [radius] - Fires Monster Overhaul: start a healing pattern on the nearest boss this peer owns, for tests", args =>
            {
                if (args.Length < 2) { args.Context?.AddString("fmo_boss_pattern: say tethered, channelled, feeding, shelled or end"); return; }
                string what = args[1].ToLowerInvariant();
                bool end = what == "end";
                HealPattern pattern = HealPattern.None;
                if (!end && !Enum.TryParse(args[1], true, out pattern) || (!end && pattern == HealPattern.None))
                {
                    args.Context?.AddString($"fmo_boss_pattern: '{args[1]}' is not tethered, channelled, feeding, shelled or end");
                    return;
                }
                string feeder = null;
                float radius = DefaultRadius;
                for (int i = 2; i < args.Length; i++)
                {
                    if (float.TryParse(args[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float r)) radius = r;
                    else feeder = args[i];
                }
                var player = Player.m_localPlayer;
                if (player == null) { args.Context?.AddString("fmo_boss_pattern: no local player"); return; }

                var near = new List<Character>();
                Character.GetCharactersInRange(player.transform.position, radius, near);
                BossDirector best = null;
                float bestD = float.MaxValue;
                int notOwned = 0;
                foreach (var c in near)
                {
                    if (c == null || c.IsDead() || !c.IsBoss()) continue;
                    var director = c.GetComponent<BossDirector>();
                    if (director == null || director.Nview == null || !director.Nview.IsValid()) continue;
                    if (!director.Nview.IsOwner()) { notOwned++; continue; }
                    float d = Vector3.Distance(c.transform.position, player.transform.position);
                    if (d < bestD) { best = director; bestD = d; }
                }
                string line;
                if (best == null) line = $"fmo_boss_pattern: no boss this peer owns within {radius:0.#} m ({notOwned} not owned here)";
                else if (end)
                {
                    var running = best.Pattern;
                    if (running == HealPattern.None) line = $"fmo_boss_pattern: {best.Prefab} has no pattern running {best.Zdo.m_uid}";
                    else
                    {
                        best.EndPattern("ended by fmo_boss_pattern", true);
                        line = $"fmo_boss_pattern: ended {running.ToString().ToLowerInvariant()} on {best.Prefab} {best.Zdo.m_uid}";
                    }
                }
                else if (best.Pattern != HealPattern.None) line = $"fmo_boss_pattern: {best.Prefab} already runs {best.Pattern.ToString().ToLowerInvariant()} (end it first) {best.Zdo.m_uid}";
                else if (!best.StartPattern(pattern, "forced by fmo_boss_pattern", feeder)) line = $"fmo_boss_pattern: {best.Prefab} refused {what} {best.Zdo.m_uid}";
                else line = $"fmo_boss_pattern: started {what} on {best.Prefab} {best.Zdo.m_uid}";
                MonsterLog.Debug(line);
                args.Context?.AddString(line);
            }, isCheat: true);
        }
    }
}
