using System.Collections.Generic;
using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// The Elder (doc 22; 34-PHASE4 slice 2): Tethered at 50 % (three Heartwood saplings round him; immune and healing
    /// while any stands) and Entangle (a ring at a player's feet; if they're still in it when it lands, roots hold them
    /// until someone breaks the roots). His vanilla stomp, vine shot and root spawns stay as they are.
    /// </summary>
    internal sealed class ElderScript : BossScript
    {
        private const int PhaseHalf = 1 << 0;
        private const float HalfHealth = 0.5f;

        private EntangleCaster _entangle;

        public override void Tick(bool inCombat, float healthFraction)
        {
            if (_entangle == null) _entangle = new EntangleCaster(D);
            if (healthFraction <= HalfHealth && D.FirePhase(PhaseHalf, "half", healthFraction))
                D.StartPattern(HealPattern.Tethered, "phase half");
            _entangle.WitherRoots();
            if (!inCombat) return;
            _entangle.TryEntangle();
        }

        public override void OnDeath() => _entangle?.OnDeath();

        public override void ResetCooldowns() => _entangle?.ResetCooldowns();
    }

    /// <summary>
    /// The Elder's Entangle, unchanged; 0.0.40 (boss phases): moved out of ElderScript so The Queen's second phase uses the
    /// same code (same config keys, same roots, same log lines) instead of a copy.
    /// </summary>
    internal sealed class EntangleCaster
    {
        private const float EntangleRange = 25f;
        private const float RootsInFront = 1.2f;  // the roots stand this far from the player, towards the Elder
        private const float RootsLinger = 1f;     // roots are removed this long after the root itself times out

        private readonly BossDirector D;
        private float _nextEntangle;
        private readonly List<(ZDOID roots, float until)> _roots = new List<(ZDOID, float)>();

        public EntangleCaster(BossDirector director) => D = director;

        public void TryEntangle()
        {
            if (Time.time < _nextEntangle) return;
            var target = PickTarget();
            if (target == null) return;
            var cast = D.Cast();
            if (cast.Active) return;
            float windup = MonsterConfig.EntangleWindupSeconds.Value;
            float radius = MonsterConfig.EntangleRadius.Value;
            Vector3 spot = target.transform.position;
            var player = target;
            if (!cast.Begin("entangle", windup, false, 3, radius, () => Land(player, spot, radius), holdStill: false, ringCenter: spot)) return;
            _nextEntangle = Time.time + MonsterConfig.EntangleCooldown.Value;
            MonsterLog.Debug($"boss: {D.Prefab} entangle at {spot:F1} on {player.GetPlayerName()} ({windup:0.0} s, r {radius:0.#} m) {D.Zdo.m_uid}");
        }

        // Its target if that's a player in range, else the nearest player in range.
        private Player PickTarget()
        {
            if (D.Target is Player p && !p.IsDead() && Vector3.Distance(p.transform.position, D.transform.position) <= EntangleRange) return p;
            Player best = null;
            float bestD = EntangleRange;
            foreach (var other in Player.GetAllPlayers())
            {
                if (other == null || other.IsDead()) continue;
                float d = Vector3.Distance(other.transform.position, D.transform.position);
                if (d <= bestD) { best = other; bestD = d; }
            }
            return best;
        }

        private void Land(Player player, Vector3 spot, float radius)
        {
            if (player == null || player.IsDead()) return;
            Vector3 p = player.transform.position;
            float off = new Vector2(p.x - spot.x, p.z - spot.z).magnitude;
            if (off > radius)
            {
                MonsterLog.Debug($"boss: {D.Prefab} entangle missed {player.GetPlayerName()} ({off:0.0} m from the spot, ring {radius:0.#} m) {D.Zdo.m_uid}");
                return;
            }
            Vector3 toBoss = D.transform.position - p;
            toBoss = RingUp.Flat(toBoss, p);   // 0.0.46: halo ring plane
            Vector3 dir = toBoss.sqrMagnitude > 0.01f ? toBoss.normalized : player.transform.forward;
            float seconds = MonsterConfig.EntangleSeconds.Value;
            float health = MonsterConfig.EntangleRootsHealth.Value;
            var roots = BossProps.Place(BossProps.EntangleRoots, p + dir * RootsInFront, Quaternion.LookRotation(-dir, RingUp.Up(p)), health, D.Zdo.m_uid);
            var rootsId = roots != null ? roots.GetZDO().m_uid : ZDOID.None;
            if (roots != null) _roots.Add((rootsId, Time.time + seconds + RootsLinger));
            Entangle.Broadcast(player.GetZDOID(), rootsId, seconds);
            MonsterLog.Debug($"boss: {D.Prefab} entangle hit {player.GetPlayerName()}: roots {rootsId} ({health:0} HP, up to {seconds:0} s) {D.Zdo.m_uid}");
        }

        public void WitherRoots()
        {
            for (int i = _roots.Count - 1; i >= 0; i--)
            {
                var (id, until) = _roots[i];
                if (!BossProps.Exists(id)) { _roots.RemoveAt(i); continue; }
                if (Time.time < until) continue;
                BossProps.Remove(id);
                _roots.RemoveAt(i);
                MonsterLog.Debug($"boss: {D.Prefab} roots {id} withered {D.Zdo.m_uid}");
            }
        }

        public void OnDeath()
        {
            foreach (var (id, _) in _roots) BossProps.Remove(id);
            _roots.Clear();
        }

        public void ResetCooldowns() => _nextEntangle = 0f;
    }
}
