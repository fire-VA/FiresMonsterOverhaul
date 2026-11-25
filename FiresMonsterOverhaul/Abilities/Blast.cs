using System.Collections;
using FiresCore.Npc.Archetypes.Effects;
using UnityEngine;

namespace FiresMonsterOverhaul.Abilities
{
    /// <summary>
    /// A telegraphed area blast every peer sees and every client resolves on its OWN player (players are owned by
    /// their own client, so nobody needs the monster that caused it): Death Burst, the Caster's Ground Blast and the
    /// Brute's charge slam. The monster's owner broadcasts it; each peer draws the ring for the delay (if any), then a
    /// local explosion effect, and each client hits its own player if it stands inside.
    /// The label starts the client's log line ("affix: death burst", "ability: ground blast").
    /// A NEGATIVE damage means a share of the hit player's own max health (-0.25 = 25 %), for boss attacks.
    /// Lanes (a boss's charge): a row of rings every peer draws (Core draws circles only), and a hit on every player
    /// still inside the lane when the charge ends.
    /// </summary>
    internal static class Blast
    {
        private const string Rpc = "FMO_Blast";
        private const string LaneRpc = "FMO_Lane";
        private const string LaneHitRpc = "FMO_LaneHit";
        private const float Height = 3f;
        private const float Push = 30f;
        private const string Fx = "fx_fireball_staff_explosion";
        private const float FxLifetime = 6f;
        private const float LaneRingSpacing = 2.5f;

        private static ZRoutedRpc _registeredOn;

        public static void EnsureRegistered()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc == _registeredOn) return;
            rpc.Register<Vector3, float, float, float, string>(Rpc, OnBlast);
            rpc.Register<Vector3, Vector3, float, float>(LaneRpc, OnLane);
            rpc.Register<Vector3, Vector3, float, float, string>(LaneHitRpc, OnLaneHit);
            _registeredOn = rpc;
        }

        public static void BroadcastLane(Vector3 from, Vector3 to, float halfWidth, float seconds)
        {
            if (ZRoutedRpc.instance == null) return;
            EnsureRegistered();
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, LaneRpc, from, to, halfWidth, seconds);
        }

        public static void BroadcastLaneHit(Vector3 from, Vector3 to, float halfWidth, float damage, string label)
        {
            if (ZRoutedRpc.instance == null) return;
            EnsureRegistered();
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, LaneHitRpc, from, to, halfWidth, damage, label ?? "lane");
        }

        private static void OnLane(long sender, Vector3 from, Vector3 to, float halfWidth, float seconds)
        {
            if (Player.m_localPlayer == null) return;
            float length = Vector3.Distance(from, to);
            int rings = Mathf.Max(1, Mathf.CeilToInt(length / LaneRingSpacing));
            for (int i = 0; i <= rings; i++)
                GroundRingManager.Show(GroundRingKind.Damage, Vector3.Lerp(from, to, i / (float)rings), halfWidth, seconds, 0L, false);
        }

        private static void OnLaneHit(long sender, Vector3 from, Vector3 to, float halfWidth, float damage, string label)
        {
            var player = Player.m_localPlayer;
            if (player == null || player.IsDead()) return;
            Vector3 p = player.transform.position;
            // 0.0.46 (halo ring): the same lane test in the ring's floor plane, height along the ring's up
            if (RingUp.OnRing(p, out Vector3 up))
            {
                Vector3 lane = to - from, rel = p - from;
                Vector3 laneF = lane - up * Vector3.Dot(lane, up), relF = rel - up * Vector3.Dot(rel, up);
                float tr = laneF.sqrMagnitude > 0.01f ? Mathf.Clamp01(Vector3.Dot(relF, laneF) / laneF.sqrMagnitude) : 0f;
                float offR = (relF - laneF * tr).magnitude;
                Vector3 onLane = from + lane * tr;
                if (offR > halfWidth + player.GetRadius() || Mathf.Abs(Vector3.Dot(p - onLane, up)) > Height)
                {
                    MonsterLog.Debug($"{label} missed {player.GetPlayerName()} ({offR:0.0} m from the lane, half-width {halfWidth:0.#} m, ring)");
                    return;
                }
                RingUp.Proof("blast lane", p, up);
                Hit(player, onLane + up * Vector3.Dot(p - onLane, up), damage, label, offR);
                return;
            }
            Vector3 a = new Vector3(from.x, 0f, from.z), b = new Vector3(to.x, 0f, to.z), q = new Vector3(p.x, 0f, p.z);
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 0.01f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            float off = Vector3.Distance(q, a + ab * t);
            if (off > halfWidth + player.GetRadius() || Mathf.Abs(p.y - Mathf.Lerp(from.y, to.y, t)) > Height) // the body counts, not just its centre (round 14: 1.1 m off a 1 m half-width read as a miss)
            {
                MonsterLog.Debug($"{label} missed {player.GetPlayerName()} ({off:0.0} m from the lane, half-width {halfWidth:0.#} m)");
                return;
            }
            Hit(player, a + ab * t + Vector3.up * p.y, damage, label, off);
        }

        public static void Broadcast(Vector3 pos, float radius, float delay, float damage, string label)
        {
            if (ZRoutedRpc.instance == null) return;
            EnsureRegistered();
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, pos, radius, delay, damage, label ?? "blast");
        }

        private static void OnBlast(long sender, Vector3 pos, float radius, float delay, float damage, string label)
        {
            if (Player.m_localPlayer == null || Plugin.Instance == null) return; // a dedicated server has nobody to hit
            if (delay > 0f) GroundRingManager.Show(GroundRingKind.Damage, pos, radius, delay, 0L, false);
            Plugin.Instance.StartCoroutine(Detonate(pos, radius, delay, damage, label));
        }

        private static IEnumerator Detonate(Vector3 pos, float radius, float delay, float damage, string label)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            SpawnLocalFx(pos);
            var player = Player.m_localPlayer;
            if (player == null || player.IsDead()) yield break;
            Vector3 d = player.transform.position - pos;
            float flat = new Vector2(d.x, d.z).magnitude;
            if (flat > radius || Mathf.Abs(d.y) > Height)
            {
                MonsterLog.Debug($"{label} at {pos:F1} missed {player.GetPlayerName()} ({flat:0.0} m from the centre, ring {radius:0.#} m)");
                yield break;
            }
            Hit(player, pos, damage, label, flat);
        }

        private static void Hit(Player player, Vector3 from, float damage, string label, float off)
        {
            float amount = damage < 0f ? -damage * player.GetMaxHealth() : damage;
            Vector3 d = player.transform.position - from;
            var hit = new HitData();
            hit.m_damage.m_blunt = amount;
            hit.m_point = player.GetCenterPoint();
            Vector3 dF = RingUp.Flat(d, player.transform.position);   // 0.0.46: the push in the halo ring plane (exactly (d.x, 0, d.z) off a ring)
            hit.m_dir = dF.sqrMagnitude > 0.0001f ? dF.normalized : RingUp.Up(player.transform.position);
            hit.m_pushForce = Push;
            hit.m_blockable = true;
            hit.m_dodgeable = true;
            hit.m_hitType = HitData.HitType.EnemyHit;
            float before = player.GetHealth();
            player.Damage(hit);
            MonsterLog.Debug($"{label} at {from:F1} hit {player.GetPlayerName()} for {amount:0.#} ({off:0.0} m from the centre; HP {before:0.#} -> {player.GetHealth():0.#})");
        }

        // The explosion effect, made on this peer only (never a networked object).
        private static void SpawnLocalFx(Vector3 pos)
        {
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(Fx) : null;
            if (prefab == null) return;
            bool previous = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            try { Object.Destroy(Object.Instantiate(prefab, pos, Quaternion.identity), FxLifetime); }
            finally { ZNetView.m_forceDisableInit = previous; }
        }
    }
}
