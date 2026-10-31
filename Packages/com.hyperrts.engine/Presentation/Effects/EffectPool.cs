using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Common;
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
        private Camera _camera;

        /// <summary>The camera position taken by the last <see cref="Tick"/>; the origin without a camera.</summary>
        public Vector3 Eye { get; private set; }

        /// <summary>Whether the last <see cref="Tick"/> found a camera.</summary>
        public bool HasEye { get; private set; }

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
            rig.Capture();
            _rigs[prefab] = rig;
            rig.Alternatives = Array.ConvertAll(rig.variants, Get);
            return rig;
        }

        /// <summary>Bursts the prefab's rig at the pose when it is within the rig's range of the camera.</summary>
        public void Play(GameObject prefab, Vector3 position, Quaternion rotation, float splashRadius = 0f)
        {
            var rig = Get(prefab);
            if (rig != null && InRange(rig, position))
            {
                rig.Play(position, rotation, splashRadius);
            }
        }

        /// <summary>Whether the rig at the position is within <paramref name="reach"/> × its range.</summary>
        public bool InRange(EffectRig rig, Vector3 position, float reach = 1f)
        {
            var limit = rig.maxDistance * reach;
            return HasEye && (position - Eye).sqrMagnitude <= limit * limit;
        }

        /// <summary>Takes this frame's camera position, fires due timed bursts and fades flashes.</summary>
        public void Tick(float deltaTime)
        {
            // Nothing plays without a camera, since nobody would see it.
            _camera = MainCamera.Find(_camera);
            HasEye = _camera != null;
            Eye = HasEye ? _camera.transform.position : Vector3.zero;

            foreach (var rig in _rigs.Values)
            {
                rig.Tick();
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
