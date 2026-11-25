using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace FiresMonsterOverhaul.Perf
{
    /// <summary>
    /// 'fmo_perf [seconds]' (cheat): samples FMO's per-frame hot paths for N seconds (default 20) and prints, per path,
    /// the calls and microseconds per frame. 'fmo_perf cache on|off' switches the 0.0.26 caches for an A/B in one session
    /// (default on). Off the sample window nothing is timed (one bool check per call).
    /// </summary>
    internal static class FmoPerf
    {
        public enum Path { UpdateAI, MaxHealthBase, EnemyHud, GroupScaler, AffixRunner, MonsterBrain, VeteranAbilities, MonsterCast, BossDirector }

        public static bool Sampling;
        public static bool Caches = true;
        private static readonly long[] Ticks = new long[System.Enum.GetValues(typeof(Path)).Length];
        private static readonly long[] Calls = new long[Ticks.Length];

        public static long Start() => Sampling ? Stopwatch.GetTimestamp() : 0L;

        public static void Stop(Path p, long start)
        {
            if (!Sampling || start == 0L) return;
            Ticks[(int)p] += Stopwatch.GetTimestamp() - start;
            Calls[(int)p]++;
        }

        public static void RegisterCommand()
        {
            new Terminal.ConsoleCommand("fmo_perf", "[seconds] | cache on|off - Fires Monster Overhaul: time the per-frame hot paths (calls and us per frame)", args =>
            {
                if (args.Length >= 3 && args[1].ToLowerInvariant() == "cache")
                {
                    Caches = args[2].ToLowerInvariant() != "off";
                    args.Context?.AddString($"fmo_perf: caches {(Caches ? "ON" : "OFF")}");
                    MonsterLog.Info($"fmo_perf: caches {(Caches ? "ON" : "OFF")}");
                    return;
                }
                if (Sampling) { args.Context?.AddString("fmo_perf: already sampling"); return; }
                float seconds = args.Length >= 2 && float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s) ? Mathf.Clamp(s, 2f, 300f) : 20f;
                Plugin.Instance.StartCoroutine(Sample(seconds, args.Context));
            }, isCheat: true);
        }

        private static IEnumerator Sample(float seconds, Terminal console)
        {
            System.Array.Clear(Ticks, 0, Ticks.Length);
            System.Array.Clear(Calls, 0, Calls.Length);
            int frames0 = Time.frameCount;
            float t0 = Time.realtimeSinceStartup;
            int creatures = Character.GetAllCharacters().Count(c => c != null && !c.IsPlayer());
            Sampling = true;
            console?.AddString($"fmo_perf: sampling {seconds:0} s, caches {(Caches ? "ON" : "OFF")}, {creatures} creature(s) loaded");
            while (Time.realtimeSinceStartup - t0 < seconds) yield return null;
            Sampling = false;
            int frames = Mathf.Max(1, Time.frameCount - frames0);
            double usPerTick = 1e6 / Stopwatch.Frequency;
            var parts = new List<string>();
            double total = 0;
            foreach (Path p in System.Enum.GetValues(typeof(Path)))
            {
                double us = Ticks[(int)p] * usPerTick / frames;
                total += us;
                parts.Add($"{p} {Calls[(int)p] / (double)frames:0.#} calls {us:0.0} us");
            }
            string line = $"fmo_perf: {frames} frames in {Time.realtimeSinceStartup - t0:0} s, {creatures} creature(s), caches {(Caches ? "ON" : "OFF")}: total {total:0.0} us/frame; per frame: {string.Join("; ", parts)}";
            MonsterLog.Info(line);
            console?.AddString(line);
        }
    }
}
