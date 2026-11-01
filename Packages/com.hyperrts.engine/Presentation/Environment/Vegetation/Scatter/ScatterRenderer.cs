using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Presentation.Environment.Vegetation.Scatter
{
    /// <summary>
    /// Base of the renderers that scatter instances on the GPU across a map's tiles for each camera about to render,
    /// in edit mode too. It owns the build and release, the per-camera batches, the tile culling and when to regrow;
    /// a subclass owns its inputs, its batches and the jobs each visible tile queues.
    /// </summary>
    public abstract class ScatterRenderer<TBatch> : PresentationBehaviour where TBatch : ScatterBatch
    {
        [Tooltip("The compute shader that grows the instances.")]
        public ComputeShader compute;

        [Tooltip("The terrain the instances stand on.")]
        public Terrain terrain;

        private Func<ScatterCameraState<TBatch>> _newState;
        private bool _ready;
#if UNITY_EDITOR
        private bool _dirty;
#endif

        private protected CameraStates<ScatterCameraState<TBatch>> Cameras { get; } = new();

        private protected ScatterTiles Tiles { get; private set; }

        /// <summary>The quality knobs this frame's plants are scattered for.</summary>
        private protected abstract ScatterSettings Settings { get; }

        private protected abstract ScatterField Field { get; }

        private protected abstract ScatterComputeInputs Inputs { get; }

        /// <summary>Whether the plants follow something that changes every frame, so even a still camera regrows.</summary>
        private protected virtual bool RegrowsEveryFrame => false;

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

        private protected abstract List<TBatch> NewBatches();

        private protected abstract void ReleaseInputs();

        /// <summary>Hands every visible tile's jobs to the camera's batches.</summary>
        private protected abstract void Queue(List<TBatch> batches, Vector3 position, ScatterSettings settings);

        /// <summary>Adds what is wrong with the subclass's own references.</summary>
        private protected abstract void AddProblems(List<string> problems);

#if UNITY_EDITOR
        /// <summary>Whether the subclass's own references went missing while the scene stayed open.</summary>
        private protected virtual bool ReferencesStale() => false;

        /// <summary>Whether a map rebuild changed what a camera's batches draw.</summary>
        private protected abstract bool Stale(List<TBatch> batches);
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
            Tiles = Field.NewTiles(terrain);
            _newState ??= () => new ScatterCameraState<TBatch>(NewBatches());
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

            if (!_ready || !CameraStates<ScatterCameraState<TBatch>>.Draws(camera, gameObject))
            {
                return;
            }

            var state = Cameras.Get(camera, _newState);
            var position = camera.transform.position;
            var settings = Settings;
            Tiles.FindVisible(camera, position, settings.Distance);
            if (RegrowsEveryFrame || !state.Holds(position, settings, Tiles.Visible))
            {
                state.Begin();
                Queue(state.Batches, position, settings);
                Inputs.SetCamera(position, settings);
                state.Generate(position, settings, Tiles.Visible);
            }

            state.Draw(gameObject.layer, camera);
        }

#if UNITY_EDITOR
        // Catches a map rebuild that reshaped the baked data or its models while the scene stayed open.
        private bool Stale()
        {
            if (compute == null || terrain == null || Field == null || ReferencesStale())
            {
                return true;
            }

            if (Inputs.Stale || Tiles.Count != Field.tiles.Count)
            {
                return true;
            }

            foreach (var state in Cameras.All)
            {
                if (Stale(state.Batches))
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
