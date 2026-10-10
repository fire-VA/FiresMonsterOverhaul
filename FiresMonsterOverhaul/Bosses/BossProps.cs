using FiresMonsterOverhaul.Abilities;
using UnityEngine;

namespace FiresMonsterOverhaul.Bosses
{
    /// <summary>
    /// The boss fights' breakable objects, registered on every peer from the ZNetScene.Awake patch:
    /// - FMO_Heartwood: a Tethered source (the Elder's Heartwood sapling). A Greydwarf nest's slim breakable body with a
    ///   vanilla beech sapling's model scaled up on top.
    /// - FMO_EntangleRoots: Entangle's roots, the vanilla breakable roots that grow by Greydwarf nests, without their loot.
    /// Their health is set per object on its ZDO when placed.
    /// </summary>
    internal static class BossProps
    {
        public const string Heartwood = "FMO_Heartwood";
        public const string EntangleRoots = "FMO_EntangleRoots";
        private const string NestBody = "Spawner_GreydwarfNest";
        private const string SaplingModel = "Beech_Sapling";
        private const string RootsBody = "Greydwarf_Root";

        private static GameObject _heartwood;
        private static GameObject _roots;

        public static void Register(ZNetScene scene)
        {
            if (scene == null) return;
            if (_heartwood == null) _heartwood = BreakableProp.Build(scene, Heartwood, NestBody, SaplingModel, MonsterConfig.HeartwoodScale.Value);
            if (_roots == null) _roots = BreakableProp.Build(scene, EntangleRoots, RootsBody, null, 1f);
            BreakableProp.Register(scene, Heartwood, _heartwood);
            BreakableProp.Register(scene, EntangleRoots, _roots);
        }

        /// <summary>On the boss's owner: one prop on the ground, with its health and its boss on its ZDO.</summary>
        public static ZNetView Place(string prefabName, Vector3 at, Quaternion rotation, float health, ZDOID boss)
        {
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;
            if (prefab == null) return null;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(at, out float y)) at.y = y;
            var go = Object.Instantiate(prefab, at, rotation);
            var nv = go.GetComponent<ZNetView>();
            if (nv == null || !nv.IsValid()) return null;
            nv.GetZDO().Set(ZDOVars.s_health, health);
            nv.GetZDO().Set(BossDirector.ZdoPropOf, boss);
            return nv;
        }

        /// <summary>Removes a placed prop by its ZDO (any peer may take it over to remove it).</summary>
        public static void Remove(ZDOID id)
        {
            if (id == ZDOID.None || ZDOMan.instance == null) return;
            var zdo = ZDOMan.instance.GetZDO(id);
            if (zdo == null) return;
            zdo.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(zdo);
        }

        public static bool Exists(ZDOID id) => id != ZDOID.None && ZDOMan.instance != null && ZDOMan.instance.GetZDO(id) != null;
    }
}
