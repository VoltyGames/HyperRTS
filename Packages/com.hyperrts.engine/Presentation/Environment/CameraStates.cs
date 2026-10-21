using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>A scatter renderer's GPU state per camera, freed once its camera is destroyed.</summary>
    internal sealed class CameraStates<T> : IDisposable where T : class, IDisposable
    {
        private readonly Dictionary<Camera, T> _states = new();

        public IEnumerable<T> All => _states.Values;

        /// <summary>
        /// Game and Scene view cameras that see the owner's layer and scene. Reflection probes and asset preview
        /// cameras are skipped: they render other scenes or bake once, and would regrow every plant for nothing.
        /// </summary>
        public static bool Draws(Camera camera, GameObject owner)
        {
            var kind = camera.cameraType is CameraType.Game or CameraType.SceneView;
            var seesLayer = (camera.cullingMask & (1 << owner.layer)) != 0;
            var ownScene = !camera.scene.IsValid() || camera.scene == owner.scene;
            return kind && seesLayer && ownScene;
        }

        public bool TryGet(Camera camera, out T state) => _states.TryGetValue(camera, out state);

        public T Get(Camera camera, Func<T> create)
        {
            if (_states.TryGetValue(camera, out var state))
            {
                return state;
            }

            ForgetClosedCameras();
            state = create();
            _states.Add(camera, state);
            return state;
        }

        public void Dispose()
        {
            foreach (var state in _states.Values)
            {
                state.Dispose();
            }

            _states.Clear();
        }

        // Frees the buffers of cameras destroyed since, such as a closed Scene view.
        private void ForgetClosedCameras()
        {
            var closed = new List<Camera>();
            foreach (var camera in _states.Keys)
            {
                if (camera == null)
                {
                    closed.Add(camera);
                }
            }

            foreach (var camera in closed)
            {
                _states[camera].Dispose();
                _states.Remove(camera);
            }
        }
    }
}
