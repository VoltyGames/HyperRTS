using System;
using System.Collections.Generic;
using UnityEngine;
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

        /// <summary>The share of authored particles emitted at a detail level.</summary>
        public static float Density(EffectDetailLevel level) => level switch
        {
            EffectDetailLevel.Low => 0.35f,
            EffectDetailLevel.Medium => 0.7f,
            _ => 1f,
        };

        /// <summary>The prefab's live rig, or null when the prefab is broken (logged once, naming it).</summary>
        public EffectRig Get(GameObject prefab)
        {
            if (_rigs.TryGetValue(prefab, out var rig) || _broken.Contains(prefab))
            {
                return rig;
            }

            if (!prefab.TryGetComponent<EffectRig>(out _))
            {
                return Broken(prefab, $"Effect prefab '{prefab.name}' has no EffectRig on its root.");
            }

            rig = Spawn(prefab);
            try
            {
                rig.Prepare();
            }
            catch (InvalidOperationException error)
            {
                Destroy(rig.gameObject);
                return Broken(prefab, error.Message);
            }

            return _rigs[prefab] = rig;
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
                Destroy(_root);
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

        private static void Destroy(GameObject target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
