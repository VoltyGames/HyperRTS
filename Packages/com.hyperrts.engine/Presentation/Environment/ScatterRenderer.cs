using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// Base of the renderers that scatter instances on the GPU across a map's tiles for each camera about to render,
    /// in edit mode too. It owns the build and release, the per-camera states and the tile culling; a subclass owns
    /// its inputs, what each camera grows and how it draws.
    /// </summary>
    public abstract class ScatterRenderer<TState> : PresentationBehaviour where TState : class, IDisposable
    {
        [Tooltip("The compute shader that grows the instances.")]
        public ComputeShader compute;

        [Tooltip("The terrain the instances stand on.")]
        public Terrain terrain;

        private Func<TState> _newState;
        private bool _ready;
#if UNITY_EDITOR
        private bool _dirty;
#endif

        private protected CameraStates<TState> Cameras { get; } = new();

        private protected ScatterTiles Tiles { get; private set; }

        /// <summary>Metres from the camera tiles are drawn to.</summary>
        private protected abstract float DrawDistance { get; }

        protected void OnEnable()
        {
            Build();
            RenderPipelineManager.beginCameraRendering += Render;
        }

        protected override void OnDisabled()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            Release();
        }

#if UNITY_EDITOR
        // Rebuilt on the next camera, not here: OnValidate runs mid-import, too early to make GPU buffers.
        protected void OnValidate() => _dirty = true;
#endif

        /// <summary>Makes the GPU inputs every camera shares, once the references check out.</summary>
        private protected abstract void BuildInputs();

        private protected abstract ScatterTiles NewTiles();

        private protected abstract TState NewState();

        private protected abstract void ReleaseInputs();

        /// <summary>Regrows the camera's state when its view changed, then draws it.</summary>
        private protected abstract void Draw(TState state, Camera camera, Vector3 position);

        /// <summary>Adds what is wrong with the subclass's own references.</summary>
        private protected abstract void AddProblems(List<string> problems);

#if UNITY_EDITOR
        /// <summary>Whether a map rebuild reshaped the subclass's data or inputs while the scene stayed open.</summary>
        private protected abstract bool InputsStale();

        /// <summary>Whether a map rebuild changed what the camera's state draws.</summary>
        private protected abstract bool Stale(TState state);
#endif

        private void Build()
        {
#if UNITY_EDITOR
            _dirty = false;
#endif
            var problems = Problems();
            if (problems.Count > 0)
            {
                Debug.LogError($"HyperRTS: {GetType().Name} can't draw: {string.Join("; ", problems)}.", this);
                return;
            }

            BuildInputs();
            Tiles = NewTiles();
            _newState ??= NewState;
            _ready = true;
        }

        private void Release()
        {
            _ready = false;
            Cameras.Dispose();
            ReleaseInputs();
        }

        private void Render(ScriptableRenderContext context, Camera camera)
        {
#if UNITY_EDITOR
            if (_dirty || (_ready && Stale()))
            {
                Release();
                Build();
            }
#endif

            if (!_ready || !CameraStates<TState>.Draws(camera, gameObject))
            {
                return;
            }

            var state = Cameras.Get(camera, _newState);
            var position = camera.transform.position;
            Tiles.FindVisible(camera, position, DrawDistance);
            Draw(state, camera, position);
        }

#if UNITY_EDITOR
        private bool Stale()
        {
            if (compute == null || terrain == null || InputsStale())
            {
                return true;
            }

            foreach (var state in Cameras.All)
            {
                if (Stale(state))
                {
                    return true;
                }
            }

            return false;
        }
#endif

        private List<string> Problems()
        {
            var problems = new List<string>();
            AddProblems(problems);
            if (compute == null)
            {
                problems.Add($"{nameof(compute)} is missing");
            }

            if (terrain == null)
            {
                problems.Add($"{nameof(terrain)} is missing");
            }

            return problems;
        }
    }
}
