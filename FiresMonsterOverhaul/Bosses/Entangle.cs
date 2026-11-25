using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// The Elder's Entangle (doc 22: "roots a player until allies break it"). The boss's owner places breakable roots by
    /// the player and broadcasts it; the player's own client (players are owned by their clients) roots its player:
    /// the "FMO_Entangled" status effect, which holds the player's movement input (the Player.SetControls patch). The
    /// rooted player can still attack, block and turn, so a solo player can break their own roots. It ends when the roots
    /// break, when it times out, or when Purify cleanses it (Core 0.2.328+).
    /// Core's root (StatusEffectManager.ApplyRoot) only zeroes AI movement; a player would keep walking.
    /// </summary>
    internal static class Entangle
    {
        private const string Rpc = "FMO_Entangle";
        private static ZRoutedRpc _registeredOn;

        public static void EnsureRegistered()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc == _registeredOn) return;
            rpc.Register<ZDOID, ZDOID, float>(Rpc, OnEntangle);
            _registeredOn = rpc;
        }

        public static void Broadcast(ZDOID player, ZDOID roots, float seconds)
        {
            if (ZRoutedRpc.instance == null) return;
            EnsureRegistered();
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, Rpc, player, roots, seconds);
        }

        private static void OnEntangle(long sender, ZDOID playerId, ZDOID roots, float seconds)
        {
            var player = Player.m_localPlayer;
            if (player == null || player.IsDead() || player.GetZDOID() != playerId) return;
            var se = player.GetSEMan().AddStatusEffect(EntangledEffect.Hash, true);
            if (se != null) se.m_ttl = seconds;
            var watch = player.GetComponent<EntangleWatch>() ?? player.gameObject.AddComponent<EntangleWatch>();
            watch.Begin(roots, seconds);
            MonsterLog.Debug($"ability: entangle rooted {player.GetPlayerName()} (roots {roots}, up to {seconds:0} s)");
        }
    }

    /// <summary>"Entangled": no moving, running or dodging (the Player.SetControls patch); everything else still works.</summary>
    public sealed class EntangledEffect : SE_Stats
    {
        public const string EffectName = "FMO_Entangled";
        public static readonly int Hash = EffectName.GetStableHashCode();

        public static void Register(ObjectDB db)
        {
            if (db == null || db.m_StatusEffects == null) return;
            if (db.m_StatusEffects.Exists(s => s != null && s.name == EffectName)) return;
            var se = CreateInstance<EntangledEffect>();
            se.name = EffectName;
            se.m_name = "Entangled";
            se.m_tooltip = "Roots hold you in place. Break them (anyone can), or cleanse it.";
            se.m_ttl = MonsterConfig.EntangleSeconds.Value;
            var tared = db.GetStatusEffect("Tared".GetStableHashCode());
            var poison = db.GetStatusEffect("Poison".GetStableHashCode());
            se.m_icon = tared != null ? tared.m_icon : poison != null ? poison.m_icon : null;
            db.m_StatusEffects.Add(se);
            Affixes.PurifyHook.Register(EffectName);
        }
    }

    /// <summary>On the rooted player: lets go when the roots are gone, and reports how it ended.</summary>
    internal sealed class EntangleWatch : MonoBehaviour
    {
        private const float Poll = 0.25f;
        // 0.0.43 (REAL MP bug, ghost r7 "entangle on Vafire broken after 0.0 s"): when another peer owns the boss, the root RPC
        // reaches this client before the roots' ZDO does, so "roots gone" counts only once they were seen here, or after this grace.
        private const float RootsArriveGrace = 3f;
        private ZDOID _roots;
        private float _start, _until, _nextPoll;
        private bool _active, _seen;

        public void Begin(ZDOID roots, float seconds)
        {
            _roots = roots;
            _start = Time.time;
            _until = Time.time + seconds;
            _active = true;
            _seen = BossProps.Exists(roots);   // 0.0.43: false while the roots' ZDO is still on its way from the boss's owner
        }

        private void Update()
        {
            if (!_active || Time.time < _nextPoll) return;
            _nextPoll = Time.time + Poll;
            var player = GetComponent<Player>();
            if (player == null) { _active = false; return; }
            var seman = player.GetSEMan();
            string how = null;
            if (player.IsDead()) how = "died";
            else if (!BossProps.Exists(_roots) && (_seen || Time.time - _start >= RootsArriveGrace))
                how = _seen ? "broken" : "broken (its roots never arrived here)";   // 0.0.43: wait for the roots' ZDO first
            else if (!_seen && BossProps.Exists(_roots)) _seen = true;   // 0.0.43: the roots arrived; then the checks below
            else if (!seman.HaveStatusEffect(EntangledEffect.Hash)) how = Time.time >= _until - Poll ? "timed out" : "cleansed";
            else if (Time.time >= _until) how = "timed out";
            if (how == null) return;
            _active = false;
            seman.RemoveStatusEffect(EntangledEffect.Hash, true);
            MonsterLog.Debug($"ability: entangle on {player.GetPlayerName()} {how} after {Time.time - _start:0.0} s (roots {_roots})");
        }
    }
}
