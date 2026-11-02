using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter
{
    /// <summary>A scatter renderer's GPU state per camera, freed once its camera is destroyed.</summary>
    internal sealed class CameraStates<T> : IDisposable where T : class, IDisposable
    {
        private readonly Dictionary<Camera, T> _states = new();

        public IEnumerable<T> All => _states.Values;

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
