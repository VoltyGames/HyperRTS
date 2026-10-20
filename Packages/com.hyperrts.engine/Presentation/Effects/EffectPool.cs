using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// One live instance per rig prefab, shared by every event that plays it (rigs simulate in world space, so a new
    /// burst never moves the old particles). The instances are DontSave and go when the pool is disposed.
    /// </summary>
    public sealed class EffectPool : IDisposable
    {
        private readonly Dictionary<GameObject, EffectRig> _rigs = new();
        private readonly HashSet<GameObject> _broken = new();
        private GameObject _root;
        private EffectLights _lights;

        /// <summary>The prefab's live rig, or null when there is no prefab or it is broken (logged once, naming it).</summary>
        public EffectRig Get(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            if (_rigs.TryGetValue(prefab, out var rig) || _broken.Contains(prefab))
            {
                return rig;
            }

            var problems = EffectRig.Problems(prefab);
            if (problems.Count > 0)
            {
                return Broken(prefab, $"Effect rig '{prefab.name}': {string.Join("; ", problems)}.");
            }

            rig = Spawn(prefab);
            rig.Prepare();
            _rigs[prefab] = rig;
            rig.Alternatives = Array.ConvertAll(rig.variants, Get);
            return rig;
        }


        /// <summary>Fires due timed bursts and fades flashes.</summary>
        public void Tick(float deltaTime)
        {
            foreach (var rig in _rigs.Values)
            {
                rig.Tick(deltaTime);
            }

            _lights?.Tick(deltaTime);
        }

        public void Dispose()
        {
            _rigs.Clear();
            _broken.Clear();
            _lights = null;
            if (_root != null)
            {
                CoreUtils.Destroy(_root);
            }
        }

        private EffectRig Spawn(GameObject prefab)
        {
            if (_root == null)
            {
                _root = new GameObject("HyperRTS Effects") { hideFlags = HideFlags.DontSave };
                _lights = new EffectLights(_root.transform);
            }

            var instance = Object.Instantiate(prefab, _root.transform);
            instance.name = prefab.name;
            foreach (var part in instance.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.hideFlags = HideFlags.DontSave;
            }

            var rig = instance.GetComponent<EffectRig>();
            rig.Lights = _lights;
            return rig;
        }

        private EffectRig Broken(GameObject prefab, string message)
        {
            _broken.Add(prefab);
            Debug.LogError(message, prefab);
            return null;
        }
    }
}
