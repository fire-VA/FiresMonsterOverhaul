using UnityEngine;

namespace FiresMonsterOverhaul.Abilities
{
    /// <summary>
    /// Networked, breakable objects FMO places in a fight (the Warded totem, the Elder's Heartwood saplings, Entangle's
    /// roots), built once per ZNetScene on every peer (the ZNetScene.Awake patch) so their ZDOs resolve everywhere.
    /// The body is a vanilla breakable prefab (its ZNetView, Destructible, colliders and hit/destroy effects) with its
    /// spawning, loot, hover text and, when a model is given, its own looks removed; the model is a vanilla prefab's
    /// visuals (scripts, colliders and physics stripped) scaled on top.
    /// </summary>
    internal static class BreakableProp
    {
        private const float DefaultHealth = 100f;
        private static GameObject _holder;

        /// <summary>Registers a built prop under <paramref name="name"/>; false when it's already there or can't be built.</summary>
        public static bool Register(ZNetScene scene, string name, GameObject prefab)
        {
            if (scene == null || prefab == null) return false;
            int hash = name.GetStableHashCode();
            if (scene.m_namedPrefabs.ContainsKey(hash)) return false;
            scene.m_prefabs.Add(prefab);
            scene.m_namedPrefabs[hash] = prefab;
            return true;
        }

        /// <param name="visualPrefab">The model's vanilla prefab, or null to keep the body's own looks.</param>
        public static GameObject Build(ZNetScene scene, string name, string bodyPrefab, string visualPrefab, float scale)
        {
            var body = scene.GetPrefab(bodyPrefab);
            var visual = visualPrefab != null ? scene.GetPrefab(visualPrefab) : null;
            if (body == null || body.GetComponent<Destructible>() == null || (visualPrefab != null && visual == null))
            {
                MonsterLog.Warning($"prop {name}: vanilla {bodyPrefab} (breakable){(visualPrefab != null ? " or " + visualPrefab : "")} not found; it won't appear");
                return null;
            }
            if (_holder == null)
            {
                _holder = new GameObject("FMO_PrefabHolder");
                _holder.SetActive(false); // nothing under it wakes up: these are prefabs, not live objects
                Object.DontDestroyOnLoad(_holder);
            }

            var prop = Object.Instantiate(body, _holder.transform);
            prop.name = name;
            foreach (var c in prop.GetComponentsInChildren<SpawnArea>(true)) Object.DestroyImmediate(c);
            foreach (var c in prop.GetComponentsInChildren<DropOnDestroyed>(true)) Object.DestroyImmediate(c);
            foreach (var c in prop.GetComponentsInChildren<HoverText>(true)) Object.DestroyImmediate(c);
            if (visual != null)
            {
                foreach (var c in prop.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(c);
                foreach (var c in prop.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(c.gameObject);
                foreach (var c in prop.GetComponentsInChildren<Renderer>(true)) Object.DestroyImmediate(c);
            }

            var destructible = prop.GetComponent<Destructible>();
            destructible.m_health = DefaultHealth;
            destructible.m_damages = default;
            destructible.m_minToolTier = 0;
            destructible.m_spawnWhenDestroyed = null;

            if (visual != null)
            {
                var model = Object.Instantiate(visual, prop.transform);
                model.name = "FMO_PropModel";
                StripToLooks(model);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * scale;
            }
            else if (scale > 0f && scale != 1f)
            {
                prop.transform.localScale = Vector3.one * scale;
            }
            return prop;
        }

        // A vanilla prefab down to its looks: scripts first (newest first, twice, so one that another requires goes once its
        // dependant is gone), then colliders and physics.
        private static void StripToLooks(GameObject model)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                var scripts = model.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = scripts.Length - 1; i >= 0; i--)
                    if (scripts[i] != null) Object.DestroyImmediate(scripts[i]);
            }
            foreach (var c in model.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var c in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);
        }
    }
}
