using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FiresMonsterOverhaul.Roles
{
    /// <summary>
    /// The creature's role, decided once on its owner (after vanilla has handed it its weapons) and kept on the ZDO,
    /// so every peer reads the same role even for creatures whose weapon is picked at random per instance.
    /// </summary>
    public sealed class MonsterRoleTag : MonoBehaviour
    {
        public static readonly int ZdoRole = "fmo_role".GetStableHashCode();
        private static readonly HashSet<string> Reported = new HashSet<string>();

        private ZNetView _nview;
        private Character _character;

        public MonsterRole Role
        {
            get
            {
                if (_nview == null || !_nview.IsValid()) return MonsterRole.None;
                return (MonsterRole)_nview.GetZDO().GetInt(ZdoRole, 0);
            }
        }

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _character = GetComponent<Character>();
        }

        private IEnumerator Start()
        {
            yield return null; // Humanoid.Start gives the default items; read the weapon after it.
            if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _character == null) yield break;
            var zdo = _nview.GetZDO();
            if (zdo.GetInt(ZdoRole, -1) >= 0 && zdo.GetBool("fmo_role_set")) yield break;
            var role = RoleRegistry.Resolve(_character, out string why);
            zdo.Set(ZdoRole, (int)role);
            zdo.Set("fmo_role_set", true);
            string prefab = Utils.GetPrefabName(gameObject);
            if (Reported.Add(prefab + "|" + role))
                MonsterLog.Debug($"role: {prefab} -> {role} ({why})");
        }
    }
}
