using UnityEngine;

namespace FiresMonsterOverhaul.Affixes
{
    /// <summary>
    /// The Warded affix's totem, built once per ZNetScene on every peer (the ZNetScene.Awake patch) so its ZDOs resolve
    /// everywhere. Fire 10-08: "use the vanilla game's goblin king totem prefab ... just scale it". It's a vanilla
    /// Greydwarf nest's networked, breakable body (its Destructible, colliders and hit/destroy effects), with the nest's
    /// spawning, loot and looks removed, and the vanilla Fuling totem's model (GoblinTotem) scaled up on top.
    /// </summary>
    internal static class WardTotem
    {
        public const string PrefabName = "FMO_WardTotem";
        private const string BasePrefab = "Spawner_GreydwarfNest";
        private const string VisualPrefab = "GoblinTotem";
        private const float DefaultHealth = 100f;

        private static GameObject _holder;
        private static GameObject _prefab;

        public static void Register(ZNetScene scene)
        {
            if (scene == null) return;
            int hash = PrefabName.GetStableHashCode();
            if (scene.m_namedPrefabs.ContainsKey(hash)) return;
            if (_prefab == null) _prefab = Build(scene);
            if (_prefab == null) return;
            scene.m_prefabs.Add(_prefab);
            scene.m_namedPrefabs[hash] = _prefab;
        }

        private static GameObject Build(ZNetScene scene)
        {
            var basePrefab = scene.GetPrefab(BasePrefab);
            var visual = scene.GetPrefab(VisualPrefab);
            if (basePrefab == null || visual == null || basePrefab.GetComponent<Destructible>() == null)
            {
                MonsterLog.Warning($"ward totem: vanilla {BasePrefab} (breakable) or {VisualPrefab} not found; Warded monsters raise no totem");
                return null;
            }
            if (_holder == null)
            {
                _holder = new GameObject("FMO_PrefabHolder");
                _holder.SetActive(false); // nothing under it wakes up: these are prefabs, not live objects
                Object.DontDestroyOnLoad(_holder);
            }

            var ward = Object.Instantiate(basePrefab, _holder.transform);
            ward.name = PrefabName;
            foreach (var c in ward.GetComponentsInChildren<SpawnArea>(true)) Object.DestroyImmediate(c);
            foreach (var c in ward.GetComponentsInChildren<DropOnDestroyed>(true)) Object.DestroyImmediate(c);
            foreach (var c in ward.GetComponentsInChildren<HoverText>(true)) Object.DestroyImmediate(c);
            foreach (var c in ward.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(c);
            foreach (var c in ward.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(c.gameObject);
            foreach (var c in ward.GetComponentsInChildren<Renderer>(true)) Object.DestroyImmediate(c);

            var destructible = ward.GetComponent<Destructible>();
            destructible.m_health = DefaultHealth;
            destructible.m_damages = default;
            destructible.m_minToolTier = 0;
            destructible.m_spawnWhenDestroyed = null;

            var model = Object.Instantiate(visual, ward.transform);
            model.name = "FMO_TotemModel";
            foreach (var c in model.GetComponentsInChildren<ItemDrop>(true)) Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<ZSyncTransform>(true)) Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<ZNetView>(true)) Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<Floating>(true)) Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * MonsterConfig.WardTotemScale.Value;
            return ward;
        }
    }
}
