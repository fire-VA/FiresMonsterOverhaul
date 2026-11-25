using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// 0.0.40 (Fire: boss phases for Moder, Yagluth, The Queen and Fader): "fmo_boss_phase_test [moder|yagluth|queen|fader|all]".
    /// Admin (cheat), Test worlds only (the FDT TestSite.IsTestWorld rule). For each boss: spawns it 20 m in front of the
    /// player (so this peer owns it and runs its BossDirector), sets its health just under each phase threshold through
    /// Character.SetHealth (the owner's normal health path; the director's own tick then fires the phase), waits a measured
    /// settle for the phase line (BossDirector.PhaseFired, raised next to the "phase" Debug line), then removes the boss,
    /// its adds and its roots. Prints BEGIN / "n/N &lt;boss&gt; phase k PASS|FAIL - detail" / END PASS|FAIL; 0 compared is a FAIL.
    /// The player's health is put back at the end (the phases' rings may land on them).
    /// 0.0.42 (Fire: cover every boss): also Eikthyr (half), the Elder / gd_king (half) and Bonemass (two thirds, one
    /// third), so "all" runs all 7; the Elder adds a check that its Entangle (the shared EntangleCaster) fires after its
    /// phase: its cooldown is reset (BossDirector.ResetCooldowns) and its "boss: gd_king entangle at" Debug line for that
    /// boss is read in memory through a BepInEx ILogListener (as FiresNpcsTest's ShotListener) within a measured wait.
    /// 0.0.44 (Fire: visual proof = shots he looks at): a full-resolution shot of each boss right after it spawns, right after
    /// each phase step (its adds / ring / lane / roots under way) and, for the Elder, after its Entangle step, to
    /// BepInEx\FiresTests\shots\fmo_boss_&lt;boss&gt;_&lt;moment&gt;_&lt;yyyyMMdd_HHmmss&gt;.png. Before each shot the player turns to the
    /// boss through Player.SetMouseLook (re-aimed each frame, measured, max 1.5 s); no camera override, no OS input, HUD never
    /// hidden. Aim + capture reused from FiresCompanions CompanionsHealthbarTest. The player's look is put back at the end.
    /// </summary>
    internal static class BossPhaseTest
    {
        private const string Tag = "[FmoTest] ";
        private const float SpawnDistance = 20f;
        private const float OwnSettle = 5f;      // seconds for the spawned boss's director to appear on this peer
        private const float PhaseSettle = 6f;    // seconds for a phase line (the director ticks every 0.5 s)
        private const float UnderThreshold = 0.02f;
        private const float CleanupRange = 80f;
        // 0.0.42: Entangle wait = windup + this (the director ticks every 0.5 s, the caster may wait out another cast).
        private const float EntangleExtra = 8f;
        // 0.0.44 r2 (round on SinkTestGen11: the Elder drifted to 25.9 m, past its 25 m Entangle range, before the cast):
        // the Elder spawns at 13 m; before the Entangle check, over EntangleNear the test waits (measured) for it to come
        // closer, then moves the PLAYER (Player.TeleportTo, the game's own teleport) to EntangleNear-6 m from it.
        private const float ElderSpawnDistance = 13f;
        private const float EntangleNear = 20f;
        private const float ApproachWait = 4f;
        private const float TeleportWait = 10f;

        private sealed class Boss
        {
            public string Name, Prefab;
            public Func<TwoPhaseBossScript> Script;
            public Func<bool> Enabled;
            // 0.0.42 (all 7 bosses): fixed phases for the non-TwoPhase scripts (mirrors their private consts).
            public string[] PhaseNames;
            public float[] PhaseAt;
            public bool Entangle;
            public float Spawn = SpawnDistance;   // 0.0.44 r2: the Elder spawns closer (its Entangle range is 25 m)

            public string[] Names => PhaseNames ?? new[] { TwoPhaseBossScript.FirstName, TwoPhaseBossScript.SecondName };
            public float[] At()
            {
                if (PhaseAt != null) return PhaseAt;
                var s = Script();
                return new[] { s.FirstAt.Value, s.SecondAt.Value };
            }
        }

        // 0.0.42: reads "boss: gd_king entangle at ... <uid>" lines in memory (reused from FiresNpcsTest ShotListener).
        private sealed class EntangleListener : BepInEx.Logging.ILogListener
        {
            public volatile string WantUid;
            public volatile string Line;
            // 0.0.43 (ghost r7: another peer owned the Elder, so its "entangle at" line was in THAT peer's log): also the rooted
            // player's own line, "ability: entangle rooted <name> (roots <id>", kept here; the main thread then checks the
            // roots' ZDO carries this boss (BossDirector.ZdoPropOf) before it counts.
            public volatile string WantName;
            public volatile string RootedLine;
            public void LogEvent(object sender, BepInEx.Logging.LogEventArgs e)
            {
                string want = WantUid;
                if (want == null) return;
                var msg = e?.Data?.ToString();
                if (msg == null) return;
                if (Line == null && msg.IndexOf(" entangle at ", StringComparison.Ordinal) >= 0 && msg.TrimEnd().EndsWith(want, StringComparison.Ordinal)) Line = msg;
                string name = WantName;
                if (RootedLine == null && name != null && msg.IndexOf("ability: entangle rooted " + name + " (roots ", StringComparison.Ordinal) >= 0) RootedLine = msg;
            }

            // 0.0.43: "(roots 1943558533:6641," -> that ZDOID; None when it does not parse.
            public static ZDOID RootsOf(string line)
            {
                if (line == null) return ZDOID.None;
                int i = line.IndexOf("(roots ", StringComparison.Ordinal);
                if (i < 0) return ZDOID.None;
                i += 7;
                int colon = line.IndexOf(':', i);
                if (colon < 0) return ZDOID.None;
                int end = colon + 1;
                while (end < line.Length && char.IsDigit(line[end])) end++;
                return long.TryParse(line.Substring(i, colon - i), out long user) && uint.TryParse(line.Substring(colon + 1, end - colon - 1), out uint id)
                    ? new ZDOID(user, id) : ZDOID.None;
            }
            public void Dispose() { }
        }

        private static readonly Boss[] All =
        {
            // 0.0.42 (all 7 bosses): thresholds from EikthyrScript / ElderScript HalfHealth and BonemassScript 2/3, 1/3.
            new Boss { Name = "eikthyr", Prefab = "Eikthyr", Enabled = () => true, PhaseNames = new[] { "half" }, PhaseAt = new[] { 0.5f } },
            new Boss { Name = "elder", Prefab = "gd_king", Enabled = () => true, PhaseNames = new[] { "half" }, PhaseAt = new[] { 0.5f }, Entangle = true, Spawn = ElderSpawnDistance },
            new Boss { Name = "bonemass", Prefab = "Bonemass", Enabled = () => true, PhaseNames = new[] { "two thirds", "one third" }, PhaseAt = new[] { 2f / 3f, 1f / 3f } },
            new Boss { Name = "moder", Prefab = "Dragon", Script = () => new ModerScript(), Enabled = () => MonsterConfig.ModerPhases.Value },
            new Boss { Name = "yagluth", Prefab = "GoblinKing", Script = () => new YagluthScript(), Enabled = () => MonsterConfig.YagluthPhases.Value },
            new Boss { Name = "queen", Prefab = "SeekerQueen", Script = () => new QueenScript(), Enabled = () => MonsterConfig.QueenPhases.Value },
            new Boss { Name = "fader", Prefab = "Fader", Script = () => new FaderScript(), Enabled = () => MonsterConfig.FaderPhases.Value },
        };

        private static bool _running;

        // Reused from FDT Utilities\TestSite.cs IsTestWorld (the team's Test-world rule).
        internal static bool IsTestWorld(string world) =>
            !string.IsNullOrEmpty(world) && (world.StartsWith("ATomTest", StringComparison.OrdinalIgnoreCase)
                || world.IndexOf("Test", StringComparison.OrdinalIgnoreCase) >= 0 || world.StartsWith("AB", StringComparison.Ordinal));

        public static void Register()
        {
            new Terminal.ConsoleCommand("fmo_boss_phase_test", "[eikthyr|elder|bonemass|moder|yagluth|queen|fader|all] - Fires Monster Overhaul: spawn each boss, force its health under each phase threshold and check the phase fires (Test worlds only)", args =>
            {
                var ctx = args.Context;
                // 0.0.43 (FDT runner passes no args to non-contract commands): no / blank / quoted-empty argument = "all"; explicit names still work.
                string what = args.Length >= 2 ? args[1].Trim().Trim('"').Trim().ToLowerInvariant() : "";
                if (what.Length == 0) what = "all";
                var picked = new List<Boss>();
                foreach (var b in All)
                    if (what == "all" || what == b.Name || what == b.Prefab.ToLowerInvariant()) picked.Add(b);
                if (picked.Count == 0) { Say(ctx, $"fmo_boss_phase_test: '{what}' is not eikthyr, elder, bonemass, moder, yagluth, queen, fader (or Eikthyr, gd_king, Bonemass, Dragon, GoblinKing, SeekerQueen, Fader) or all"); return; }
                string world = ZNet.instance != null ? ZNet.instance.GetWorldName() : null;
                if (!IsTestWorld(world)) { Say(ctx, $"fmo_boss_phase_test: refused, '{world ?? "?"}' is not a Test world"); return; }
                if (Player.m_localPlayer == null || ZNetScene.instance == null || Plugin.Instance == null) { Say(ctx, "fmo_boss_phase_test: no local player"); return; }
                if (_running) { Say(ctx, "fmo_boss_phase_test: already running"); return; }
                Plugin.Instance.StartCoroutine(Run(ctx, picked, world));
            }, isCheat: true);
        }

        private static void Say(Terminal ctx, string line)
        {
            MonsterLog.Info(Tag + line);
            ctx?.AddString(line);
        }


        // 0.0.43 (ghost r7): the run's counters and the boss in hand, shared by Run (the guard) and Steps (the checks), so an
        // exception in a step becomes a FAIL line and Run still cleans up and prints END.
        private sealed class RunState
        {
            public int Total, N, Compared, Failed;
            public string Fired;
            public ZDOID Watching = ZDOID.None;
            public Boss Current;
            public GameObject Go;
            public Character Body;
            public float PlayerHealth;
            public Player Player;
            public object LookYaw, LookPitch;   // 0.0.44: the player's look before the run, put back at the end
            public int Shots, ShotsWritten;     // 0.0.44
        }

        // ---- 0.0.44: shots (aim + capture reused from FiresCompanions Debug\CompanionsHealthbarTest.cs) ----
        private const float AimMax = 1.5f;        // seconds to turn onto the boss (re-aimed each frame)
        private const float AimTolerance = 3f;    // degrees off the camera's centre that count as "on it"
        private const float ShotWriteMax = 5f;    // seconds for the PNG to be written
        private const BindingFlags AnyField = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly FieldInfo s_lookYaw = typeof(Player).GetField("m_lookYaw", AnyField);
        private static readonly FieldInfo s_lookPitch = typeof(Player).GetField("m_lookPitch", AnyField);
        private static readonly FieldInfo s_huds = typeof(EnemyHud).GetField("m_huds", AnyField);

        // The player turns through Player.SetMouseLook (the mouse-look path: m_lookYaw / m_lookPitch); the camera follows the eye,
        // so the aim is re-taken each frame until it converges.
        private static void AimAt(Player me, Character ch)
        {
            if (me == null || ch == null || s_lookYaw == null || s_lookPitch == null) return;
            Vector3 from = GameCamera.instance != null ? GameCamera.instance.transform.position : me.GetEyePoint();
            Vector3 d = ch.GetCenterPoint() - from;
            if (d.sqrMagnitude < 0.01f) return;
            d.Normalize();
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, pitch = Mathf.Clamp(-Mathf.Asin(d.y) * Mathf.Rad2Deg, -89f, 89f);
            TurnTo(me, yaw, pitch);
        }

        private static void TurnTo(Player me, float yaw, float pitch)
        {
            float curYaw = ((Quaternion)s_lookYaw.GetValue(me)).eulerAngles.y, curPitch = (float)s_lookPitch.GetValue(me);
            me.SetMouseLook(new Vector2(Mathf.DeltaAngle(curYaw, yaw), curPitch - pitch));   // m_lookPitch -= y
        }

        private static float LookOff(Character ch) =>
            GameCamera.instance == null || ch == null ? -1f
                : Vector3.Angle(GameCamera.instance.transform.forward, ch.GetCenterPoint() - GameCamera.instance.transform.position);

        // The boss's EnemyHud entry (vanilla m_huds[character].m_gui) drawn or not, for the shot line.
        private static string BossBar(Character ch)
        {
            try
            {
                var hud = EnemyHud.instance;
                if (hud == null || s_huds == null) return "boss bar ? (no EnemyHud)";
                if (!(s_huds.GetValue(hud) is IDictionary dict) || !dict.Contains(ch)) return "boss bar NOT drawn (no EnemyHud entry)";
                var data = dict[ch];
                var gui = data?.GetType().GetField("m_gui", AnyField)?.GetValue(data) as GameObject;
                return gui != null && gui.activeInHierarchy ? "boss bar drawn" : "boss bar NOT drawn (its gui is inactive)";
            }
            catch (Exception ex) { return $"boss bar ? ({ex.GetType().Name})"; }
        }

        private static string FileSafe(string s)
        {
            var c = (s ?? "").ToLowerInvariant().ToCharArray();
            for (int i = 0; i < c.Length; i++) if (!char.IsLetterOrDigit(c[i])) c[i] = '-';
            return new string(c);
        }

        /// <summary>Turn to the boss (measured, max AimMax), then a native-size ScreenCapture after 2 end-of-frame waits.
        /// done gets "shot: &lt;path&gt; (&lt;PNG WxH&gt;, HUD shown|HIDDEN); boss bar ..., look ..." or "shot: skipped/NOT WRITTEN - why".</summary>
        private static IEnumerator Shot(RunState st, Character ch, string boss, string moment, Action<string> done)
        {
            st.Shots++;
            var me = LivePlayer();
            if (me == null) { done("shot: skipped - no live local player to look at the boss"); yield break; }
            if (ch == null || ch.gameObject == null) { done("shot: skipped - the boss is gone"); yield break; }
            float t0 = Time.realtimeSinceStartup;
            int frames = 0;
            while (Time.realtimeSinceStartup - t0 < AimMax && ch != null)
            {
                AimAt(me, ch);
                frames++;
                yield return null;
                float off = LookOff(ch);
                if (frames >= 3 && off >= 0f && off <= AimTolerance) break;
            }
            float aimed = Time.realtimeSinceStartup - t0;
            if (ch == null) { done("shot: skipped - the boss went during the aim"); yield break; }
            AimAt(me, ch);
            yield return new WaitForEndOfFrame(); yield return new WaitForEndOfFrame();
            string path;
            string look = ch != null ? $"look {LookOff(ch):0.0} deg off the boss after a {aimed:0.00} s turn, boss {Vector3.Distance(me.transform.position, ch.transform.position):0.0} m away, {BossBar(ch)}" : "boss gone at the capture";
            try
            {
                string dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "FiresTests", "shots");
                Directory.CreateDirectory(dir);
                path = Path.Combine(dir, $"fmo_boss_{FileSafe(boss)}_{FileSafe(moment)}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                if (File.Exists(path)) File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);   // native screen size (superSize 1)
            }
            catch (Exception e) { done($"shot: capture threw {e.GetType().Name}: {e.Message}"); yield break; }
            float tw = Time.realtimeSinceStartup;
            long size = -1;
            while (Time.realtimeSinceStartup - tw < ShotWriteMax)   // written at the end of the frame; wait until the size holds
            {
                long now = File.Exists(path) ? new FileInfo(path).Length : -1;
                if (now > 0 && now == size) break;
                size = now;
                yield return null;
            }
            if (!File.Exists(path)) { done($"shot: NOT WRITTEN after {ShotWriteMax:0} s ({path})"); yield break; }
            string dims = "?";
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var b = new byte[24];
                    if (fs.Read(b, 0, 24) == 24) dims = $"{(b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19]}x{(b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]}";
                }
            }
            catch { }
            st.ShotsWritten++;
            done($"shot: {path} ({dims}, HUD {(Hud.IsUserHidden() ? "HIDDEN" : "shown")}); {look}; written in {Time.realtimeSinceStartup - tw:0.00} s");
        }

        // 0.0.43: the player's live object (it may have died and respawned between bosses: r7's NRE at Queen was the old,
        // destroyed Player's transform after Yagluth's fire ring killed the tester).
        private static Player LivePlayer() => Player.m_localPlayer != null && !Player.m_localPlayer.IsDead() ? Player.m_localPlayer : null;
        private const float RespawnWait = 30f;    // 0.0.43: seconds to wait for the tester to be alive again before the next boss
        private const int MaxHits = 6;             // 0.0.43: damage hits (remote owner) to bring the boss under a threshold
        private const float HitGap = 0.5f;

        private static IEnumerator Run(Terminal ctx, List<Boss> bosses, string world)
        {
            _running = true;
            var st = new RunState();
            foreach (var b in bosses) st.Total += b.Names.Length + (b.Entangle ? 1 : 0);   // 0.0.42: per-boss phase counts
            st.Player = Player.m_localPlayer;
            st.PlayerHealth = st.Player != null ? st.Player.GetHealth() : 0f;
            try { if (st.Player != null) { st.LookYaw = s_lookYaw?.GetValue(st.Player); st.LookPitch = s_lookPitch?.GetValue(st.Player); } } catch { }   // 0.0.44
            Action<ZDOID, string> onPhase = (id, name) => { if (id == st.Watching) st.Fired = name; };
            BossDirector.PhaseFired += onPhase;
            var listener = new EntangleListener();   // 0.0.42: the Elder's Entangle line
            BepInEx.Logging.Logger.Listeners.Add(listener);
            Say(ctx, $"BEGIN fmo_boss_phase_test {Plugin.PluginName} {Plugin.PluginVersion} world '{world}': {bosses.Count} boss(es), {st.Total} checks");
            var steps = Steps(ctx, bosses, st, listener);
            bool ended = false;
            try
            {
                while (true)
                {
                    bool more;
                    try { more = steps.MoveNext(); }
                    catch (Exception ex)
                    {
                        // 0.0.43: a step that throws is a FAIL line with the reason, never an unhandled exception without END.
                        st.N++;
                        st.Failed++;
                        Say(ctx, $"{st.N}/{st.Total} {st.Current?.Name ?? "?"} FAIL - the test threw {ex.GetType().Name}: {ex.Message} ({ex.StackTrace?.Split('\n')[0].Trim()}); the rest of the run is skipped");
                        more = false;
                    }
                    if (!more) break;
                    yield return steps.Current;
                }
                ended = true;
            }
            finally
            {
                BossDirector.PhaseFired -= onPhase;
                BepInEx.Logging.Logger.Listeners.Remove(listener);
                listener.WantUid = null;
                listener.WantName = null;
                // 0.0.43: the boss in hand (a throw mid-boss) is removed too.
                if (st.Current != null) { try { Cleanup(ctx, st.Current, st.Go, st.Body); } catch (Exception ex) { Say(ctx, $"cleanup of {st.Current.Name} threw {ex.GetType().Name}: {ex.Message}"); } }
                try
                {
                    var p = LivePlayer();
                    if (p != null && p.GetHealth() < st.PlayerHealth) p.SetHealth(Mathf.Min(st.PlayerHealth, p.GetMaxHealth()));
                }
                catch (Exception ex) { Say(ctx, $"player health restore threw {ex.GetType().Name}: {ex.Message}"); }
                // 0.0.44: the player's view direction back (the live player: a respawned one gets the same look).
                try
                {
                    var p = LivePlayer();
                    if (p != null && st.LookYaw != null && st.LookPitch != null) TurnTo(p, ((Quaternion)st.LookYaw).eulerAngles.y, (float)st.LookPitch);
                }
                catch (Exception ex) { Say(ctx, $"player look restore threw {ex.GetType().Name}: {ex.Message}"); }
                Say(ctx, $"shots: {st.ShotsWritten}/{st.Shots} written to {Path.Combine(BepInEx.Paths.BepInExRootPath, "FiresTests", "shots")}");
                bool ok = ended && st.Compared > 0 && st.Failed == 0;
                Say(ctx, $"END {(ok ? "PASS" : "FAIL")} fmo_boss_phase_test: {st.Total - st.Failed}/{st.Total} passed, {st.Compared} compared{(st.Compared == 0 ? " (nothing compared)" : "")}{(ended ? "" : " (the run stopped early)")}");
                _running = false;
            }
        }

        private static bool Alive(Character c) => c != null && !c.IsDead();

        private static string OwnerOf(ZNetView nv) =>
            nv == null || !nv.IsValid() ? "gone" : nv.IsOwner() ? "this peer" : $"peer {nv.GetZDO().GetOwner()}";

        private static IEnumerator Steps(Terminal ctx, List<Boss> bosses, RunState st, EntangleListener listener)
        {
            foreach (var b in bosses)
            {
                st.Current = b;
                st.Go = null;
                st.Body = null;
                float[] at = b.At();
                string[] names = b.Names;

                string skip = !MonsterConfig.Enabled.Value || !MonsterConfig.Bosses.Value ? "boss overhaul is off in config"
                    : !b.Enabled() ? $"{b.Name} phases are off in config" : null;
                BossDirector director = null;
                ZNetView nview = null;
                ZDOID uid = ZDOID.None;

                // 0.0.43: the tester must be alive (a phase ring may have killed them on the last boss: wait for the respawn).
                Player player = null;
                if (skip == null)
                {
                    float tw = Time.time;
                    while ((player = LivePlayer()) == null && Time.time - tw < RespawnWait) yield return null;
                    if (player == null) skip = $"no live local player after {RespawnWait:0} s (dead or gone)";
                    else st.Player = player;
                }

                if (skip == null)
                {
                    var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(b.Prefab) : null;
                    if (prefab == null) skip = $"no prefab {b.Prefab}";
                    else
                    {
                        Vector3 spot = player.transform.position + player.transform.forward * b.Spawn;
                        if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(spot, out float y)) spot.y = y + 0.5f;
                        st.Go = UnityEngine.Object.Instantiate(prefab, spot, Quaternion.LookRotation(-player.transform.forward));
                        st.Body = st.Go != null ? st.Go.GetComponent<Character>() : null;
                        nview = st.Go != null ? st.Go.GetComponent<ZNetView>() : null;
                        float t0 = Time.time;
                        while (Time.time - t0 < OwnSettle && st.Go != null && (director = st.Go.GetComponent<BossDirector>()) == null) yield return null;
                        if (st.Go == null || director == null || director.Nview == null || !director.Nview.IsValid())
                            skip = $"{b.Prefab} spawned but no BossDirector after {Time.time - t0:0.0} s";
                        else
                        {
                            uid = director.Zdo.m_uid;
                            float spawnFrac = Alive(st.Body) && st.Body.GetMaxHealth() > 0f ? st.Body.GetHealth() / st.Body.GetMaxHealth() : -1f;
                            Say(ctx, $"{b.Name}: {b.Prefab} {uid} spawned, director up after {Time.time - t0:0.00} s, owner {OwnerOf(nview)}, health {spawnFrac:P0}, bits {director.Zdo.GetInt(BossDirector.ZdoPhases)}");
                            string shot = null;   // 0.0.44
                            yield return Shot(st, st.Body, b.Name, "spawn", r => shot = r);
                            Say(ctx, $"{b.Name} spawn {shot ?? "shot: no result"}");
                        }
                    }
                }

                for (int k = 0; k < names.Length; k++)
                {
                    st.N++;
                    string detail;
                    bool pass = false;
                    int bit = 1 << k;   // 0.0.43: every script's phase k is bit k (Eikthyr/Elder half = 1, Bonemass/TwoPhase 1, 2)
                    if (skip != null) detail = skip;
                    else if (!Alive(st.Body) || nview == null || !nview.IsValid()) detail = $"{b.Prefab} is gone or dead";
                    else
                    {
                        st.Compared++;
                        int before = nview.GetZDO().GetInt(BossDirector.ZdoPhases);
                        if ((before & bit) != 0)
                            detail = $"phase '{names[k]}' had already fired before the test lowered its health (bits {before}, health {st.Body.GetHealthPercentage():P0}, owner {OwnerOf(nview)})";
                        else
                        {
                            st.Watching = uid;
                            st.Fired = null;
                            float frac = Mathf.Max(0.01f, at[k] - UnderThreshold);
                            float max = st.Body.GetMaxHealth();
                            float target = max * frac;
                            string how;
                            float t0 = Time.time;
                            if (nview.IsOwner()) { st.Body.SetHealth(target); how = "SetHealth (owner here)"; }
                            else
                            {
                                // 0.0.43 (ghost r7: another peer owned Moder / Yagluth, so this peer's SetHealth was overwritten
                                // by the owner): damage, the real player's path, routed to the owner; re-hit until under.
                                int hits = 0;
                                while (hits < MaxHits && Alive(st.Body) && nview.IsValid() && st.Body.GetHealth() > target + 1f
                                       && (nview.GetZDO().GetInt(BossDirector.ZdoPhases) & bit) == 0)
                                {
                                    var hit = new HitData();
                                    hit.m_damage.m_damage = st.Body.GetHealth() - target;
                                    hit.m_point = st.Body.GetCenterPoint();
                                    hit.m_dir = Vector3.down;
                                    st.Body.Damage(hit);
                                    hits++;
                                    float tw = Time.time;
                                    while (Time.time - tw < HitGap) yield return null;
                                }
                                how = $"{hits} damage hit(s) (owner {OwnerOf(nview)})";
                            }
                            while (Time.time - t0 < PhaseSettle + (nview.IsValid() && !nview.IsOwner() ? MaxHits * HitGap : 0f) && st.Fired == null
                                   && Alive(st.Body) && nview.IsValid() && (nview.GetZDO().GetInt(BossDirector.ZdoPhases) & bit) == 0) yield return null;
                            float took = Time.time - t0;
                            int bits = nview != null && nview.IsValid() ? nview.GetZDO().GetInt(BossDirector.ZdoPhases) : -1;
                            string fired = st.Fired;
                            float now = Alive(st.Body) ? st.Body.GetHealthPercentage() : -1f;
                            if (fired == names[k]) { pass = true; detail = $"phase '{fired}' line after {took:0.00} s at {now:P0} health (threshold {at[k]:P0}, bits {bits}, {how})"; }
                            else if (fired != null) detail = $"wrong phase '{fired}' (wanted '{names[k]}') at {now:P0} health ({how})";
                            else if (bits >= 0 && (bits & bit) != 0) { pass = true; detail = $"phase '{names[k]}' bit set by the owner ({OwnerOf(nview)}) after {took:0.00} s at {now:P0} health (threshold {at[k]:P0}, bits {bits}, {how}; its line is in that peer's log)"; }
                            else if (!Alive(st.Body)) detail = $"{b.Prefab} died or went during the wait ({how})";
                            else detail = $"no phase line or bit in {took:0.0} s at {now:P0} health (threshold {at[k]:P0}, bits {bits}, {how})";
                        }
                    }
                    if (!pass) st.Failed++;
                    Say(ctx, $"{st.N}/{st.Total} {b.Name} phase {k + 1} {(pass ? "PASS" : "FAIL")} - {detail}");
                    if (skip == null)
                    {
                        // 0.0.44: right after the phase step, while its adds / ring / lane / roots are under way.
                        string shot = null;
                        yield return Shot(st, st.Body, b.Name, $"phase{k + 1}_{names[k]}", r => shot = r);
                        Say(ctx, $"{b.Name} phase {k + 1} {shot ?? "shot: no result"}");
                    }
                }

                if (b.Entangle)
                {
                    // 0.0.42: the Elder's Entangle (EntangleCaster.TryEntangle) must fire on the player after its phase.
                    st.N++;
                    string detail;
                    bool pass = false;
                    player = LivePlayer();
                    if (skip != null) detail = skip;
                    else if (!Alive(st.Body) || director == null || nview == null || !nview.IsValid()) detail = $"{b.Prefab} is gone or dead";
                    else if (player == null) detail = "no live local player to entangle";
                    else if (!MonsterLog.DebugEnabled) { st.Compared++; detail = "the entangle line is a Debug line and [Logging] Log Level is below Debug"; }
                    else
                    {
                        st.Compared++;
                        // 0.0.44 r2: in range before the cast (wait for the boss, then move the player, never the boss).
                        float d0 = Vector3.Distance(player.transform.position, st.Go.transform.position);
                        string approach = "no move needed";
                        if (d0 > EntangleNear)
                        {
                            float ta = Time.time;
                            while (Time.time - ta < ApproachWait && Alive(st.Body) && st.Go != null && player != null
                                   && Vector3.Distance(player.transform.position, st.Go.transform.position) > EntangleNear) yield return null;
                            approach = $"waited {Time.time - ta:0.0} s for the boss";
                            if (Alive(st.Body) && st.Go != null && (player = LivePlayer()) != null
                                && Vector3.Distance(player.transform.position, st.Go.transform.position) > EntangleNear)
                            {
                                Vector3 bossPos = st.Go.transform.position;
                                Vector3 away = player.transform.position - bossPos; away.y = 0f;
                                if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
                                Vector3 spot = bossPos + away.normalized * (EntangleNear - 6f);
                                if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(spot, out float gy)) spot.y = gy + 0.5f;
                                float tt = Time.time;
                                player.TeleportTo(spot, Quaternion.LookRotation(-away.normalized), false);
                                yield return null;
                                while (Time.time - tt < TeleportWait && (player = LivePlayer()) != null && player.IsTeleporting()) yield return null;
                                approach += $", then teleported the player to {EntangleNear - 6f:0} m from it in {Time.time - tt:0.0} s{(player != null && player.IsTeleporting() ? " (STILL teleporting)" : "")}";
                            }
                        }
                        player = LivePlayer();
                        float dCast = player != null && st.Go != null ? Vector3.Distance(player.transform.position, st.Go.transform.position) : -1f;
                        Say(ctx, $"{b.Name} entangle: boss {dCast:0.0} m from the player at the cast (was {d0:0.0} m; {approach}; range 25 m)");
                        listener.Line = null;
                        listener.RootedLine = null;
                        listener.WantName = player != null ? player.GetPlayerName() : null;
                        listener.WantUid = uid.ToString();
                        director.ResetCooldowns();   // only reaches the caster when this peer owns the boss
                        float wait = MonsterConfig.EntangleWindupSeconds.Value + EntangleExtra;
                        float t0 = Time.time;
                        while (Time.time - t0 < wait && listener.Line == null && listener.RootedLine == null && Alive(st.Body)) yield return null;
                        string line = listener.Line, rooted = listener.RootedLine;
                        // 0.0.43: a "rooted" line counts once its roots' ZDO (here or still arriving) names this boss.
                        string rootsCheck = null;
                        if (line == null && rooted != null)
                        {
                            var rootsId = EntangleListener.RootsOf(rooted);
                            ZDO rz = null;
                            float tr = Time.time;
                            while (rootsId != ZDOID.None && ZDOMan.instance != null && (rz = ZDOMan.instance.GetZDO(rootsId)) == null && Time.time - tr < 3f) yield return null;
                            if (rootsId == ZDOID.None) rootsCheck = "its roots id did not parse";
                            else if (rz == null) rootsCheck = $"roots {rootsId} never seen here to check their boss";
                            else if (rz.GetZDOID(BossDirector.ZdoPropOf) != uid) rootsCheck = $"roots {rootsId} belong to {rz.GetZDOID(BossDirector.ZdoPropOf)}, not {uid}";
                        }
                        float took = Time.time - t0;
                        listener.WantUid = null;
                        listener.WantName = null;
                        if (line != null) { pass = true; detail = $"entangle line after {took:0.00} s: {line.Trim()}"; }
                        else if (rooted != null && rootsCheck == null) { pass = true; detail = $"rooted by this boss's roots after {took:0.00} s (owner {OwnerOf(nview)}): {rooted.Trim()}"; }
                        else if (rooted != null) detail = $"rooted after {took:0.00} s but {rootsCheck}: {rooted.Trim()}";
                        else detail = $"no entangle line in {wait:0.0} s (player {(st.Go != null ? Vector3.Distance(player != null ? player.transform.position : Vector3.zero, st.Go.transform.position) : -1f):0.0} m away, range 25 m, owner {OwnerOf(nview)})";
                        detail += $" [at the cast {dCast:0.0} m; {approach}]";   // 0.0.44 r2
                    }
                    if (!pass) st.Failed++;
                    Say(ctx, $"{st.N}/{st.Total} {b.Name} entangle {(pass ? "PASS" : "FAIL")} - {detail}");
                    if (skip == null)
                    {
                        // 0.0.44: right after the Entangle step (its roots on the player).
                        string shot = null;
                        yield return Shot(st, st.Body, b.Name, "entangle", r => shot = r);
                        Say(ctx, $"{b.Name} entangle {shot ?? "shot: no result"}");
                    }
                }
                st.Watching = ZDOID.None;
                var cur = st.Current;
                st.Current = null;
                Cleanup(ctx, cur, st.Go, st.Body);
                st.Go = null;
                st.Body = null;
                yield return null;
            }
        }

        // The boss, its adds (AffixRunner.ZdoSummonedBy) and its roots / pattern objects (BossDirector.OnOwnerDeath) go.
        private static void Cleanup(Terminal ctx, Boss b, GameObject go, Character body)
        {
            if (go == null || ZNetScene.instance == null) return;
            var nv = go.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return;
            var uid = nv.GetZDO().m_uid;
            // 0.0.43: another peer may own the boss by now; ZNetScene.Destroy only removes the ZDO on its owner (the adds below
            // were already claimed this way), so the spawned test boss is taken back to be removed, not left in the world.
            if (!nv.IsOwner()) nv.ClaimOwnership();
            int adds = 0;
            var near = new List<Character>();
            Character.GetCharactersInRange(go.transform.position, CleanupRange, near);
            foreach (var c in near)
            {
                if (c == null || c == body || c.IsPlayer()) continue;
                var cv = c.GetComponent<ZNetView>();
                if (cv == null || !cv.IsValid() || cv.GetZDO().GetZDOID(Affixes.AffixRunner.ZdoSummonedBy) != uid) continue;
                if (!cv.IsOwner()) cv.ClaimOwnership();
                ZNetScene.instance.Destroy(c.gameObject);
                adds++;
            }
            if (body != null) BossDirector.OnOwnerDeath(body);
            ZNetScene.instance.Destroy(go);
            Say(ctx, $"{b.Name}: {b.Prefab} {uid} and {adds} add(s) removed");
        }
    }
}
