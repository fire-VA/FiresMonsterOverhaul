using System;
using System.Reflection;
using UnityEngine;

namespace FiresMonsterOverhaul
{
    /// <summary>
    /// 0.0.46 (Fire 10-24, halo ring: "some things that need gravity and 'up' are still not re targeted to the 'new' up"): the
    /// one "which way is up here" for Monster Overhaul. Core's FiresCore.World.GravityUp.InField(pos, out up), by reflection so an older
    /// Core still loads. Off the ring every caller keeps its own world-up code path (OnRing is false), so behaviour there is
    /// unchanged; on the ring rays, snaps, offsets and facing follow the ring's up.
    /// </summary>
    internal static class RingUp
    {
        private delegate bool InFieldFn(Vector3 pos, out Vector3 up);
        private static InFieldFn s_inField;
        private static bool s_looked, s_saidProof;

        private static readonly int FloorMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "Default_small", "vehicle");

        /// <summary>True when pos is inside a halo ring's gravity field; up = the ring's up there (else Vector3.up).</summary>
        internal static bool OnRing(Vector3 pos, out Vector3 up)
        {
            up = Vector3.up;
            Resolve();
            if (s_inField == null) return false;
            try
            {
                if (!s_inField(pos, out up) || up.sqrMagnitude < 0.5f) { up = Vector3.up; return false; }
                up.Normalize();
                return true;
            }
            catch { up = Vector3.up; return false; }
        }

        internal static bool OnRing(Vector3 pos) => OnRing(pos, out _);

        /// <summary>The ring's up at pos, else world up.</summary>
        internal static Vector3 Up(Vector3 pos) => OnRing(pos, out var up) ? up : Vector3.up;

        /// <summary>On the ring: the floor under pos found along -up (a ray from <paramref name="above"/> m up, <paramref name="range"/> m long).
        /// False off the ring or when nothing is hit, so the caller keeps its own snap.</summary>
        internal static bool Floor(Vector3 pos, out Vector3 floor, float above = 2f, float range = 12f, string what = null)
        {
            floor = pos;
            if (!OnRing(pos, out var up)) return false;
            if (!Physics.Raycast(pos + up * above, -up, out var hit, above + range, FloorMask, QueryTriggerInteraction.Ignore)) return false;
            floor = hit.point;
            Proof(what ?? "floor", pos, up);
            return true;
        }

        /// <summary>Standing upright at pos with this yaw: on the ring the yaw turns about the ring's up, else Euler(0, yaw, 0).</summary>
        internal static Quaternion Upright(Vector3 pos, float yawDegrees)
        {
            var flat = Quaternion.Euler(0f, yawDegrees, 0f);
            return OnRing(pos, out var up) ? Quaternion.FromToRotation(Vector3.up, up) * flat : flat;
        }

        /// <summary>v in the floor plane at <paramref name="at"/>: exactly (v.x, 0, v.z) off a ring, minus its up-component on one.</summary>
        internal static Vector3 Flat(Vector3 v, Vector3 at)
        {
            if (OnRing(at, out var up)) return v - up * Vector3.Dot(v, up);
            v.y = 0f;
            return v;
        }

        /// <summary>The component of v along up at <paramref name="at"/>: exactly v.y off a ring.</summary>
        internal static float UpDot(Vector3 v, Vector3 at) => OnRing(at, out var up) ? Vector3.Dot(v, up) : v.y;

        /// <summary>Snaps <paramref name="at"/> to the ground: on a ring the ring floor along -up (+lift along up); off it the
        /// heightmap through ZoneSystem.GetGroundHeight (+lift), exactly the old snap. False when neither found ground.</summary>
        internal static bool Ground(ref Vector3 at, float lift, string what)
        {
            if (OnRing(at, out var up))
            {
                if (!Floor(at, out var floor, 20f, 60f, what)) return false;
                at = floor + up * lift;
                return true;
            }
            if (ZoneSystem.instance == null || !ZoneSystem.instance.GetGroundHeight(at, out float y)) return false;
            at.y = y + lift;
            return true;
        }

        /// <summary>One Debug line the first time a ring path runs (the proof line).</summary>
        internal static void Proof(string what, Vector3 pos, Vector3 up)
        {
            if (s_saidProof) return;
            s_saidProof = true;
            MonsterLog.Debug($"[RingUp] {what} on a halo ring at ({pos.x:0},{pos.y:0},{pos.z:0}): up ({up.x:0.00},{up.y:0.00},{up.z:0.00}) from Core GravityUp");
        }

        private static void Resolve()
        {
            if (s_looked) return;
            s_looked = true;
            try
            {
                var t = Type.GetType("FiresCore.World.GravityUp, FiresUnifiedCore", false);
                var m = t?.GetMethod("InField", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Vector3), typeof(Vector3).MakeByRefType() }, null);
                if (m != null) s_inField = (InFieldFn)Delegate.CreateDelegate(typeof(InFieldFn), m);
                MonsterLog.Debug($"[RingUp] Core GravityUp.InField {(s_inField != null ? "bound" : "absent (older Core): world up everywhere")}");
            }
            catch (Exception ex) { MonsterLog.Warning($"[RingUp] GravityUp bind failed: {ex.Message}"); }
        }
    }
}
