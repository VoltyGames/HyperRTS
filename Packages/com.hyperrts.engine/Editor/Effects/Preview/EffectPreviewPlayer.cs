using System;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Presentation.Effects.Streams;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>
    /// Plays one effect rig the way the pool does, in edit mode, with bursts at the pivot, streams moving along a
    /// short path and fog clear.
    /// </summary>
    public sealed class EffectPreviewPlayer : IDisposable
    {
        public const float StepSeconds = 1f / 30f;
        public const float PathLength = 4f;
        public const float PathSpeed = 2f;

        private readonly Scene _scene;
        private readonly EffectPreviewEnvironment _environment = new();
        private GameObject _root;
        private EffectRig _rig;
        private GraphStream _stream;
        private VisualEffect[] _graphs = Array.Empty<VisualEffect>();

        public EffectPreviewPlayer(Scene scene) => _scene = scene;

        /// <summary>Where the rig plays; a stream's path is centred on it.</summary>
        public Vector3 Pivot { get; set; }

        public float Scale { get; set; } = 1f;

        public bool Paused { get; set; }

        public bool HasRig => _rig != null;

        /// <summary>Unpaused seconds since the last <see cref="Play"/>; the stream's place on its path.</summary>
        public float SincePlay { get; private set; }

        /// <summary>Wind blowing towards a heading in degrees from +Z, at a strength in the units the graphs read.</summary>
        public void SetWind(float degrees, float strength) => _environment.SetWind(degrees, strength);

        /// <summary>Replaces the playing instance with one of <paramref name="prefab"/>; false when it has no rig.</summary>
        public bool Load(GameObject prefab)
        {
            Unload();
            if (prefab == null || !prefab.TryGetComponent<EffectRig>(out _))
            {
                return false;
            }

            _root = Object.Instantiate(prefab);
            _root.name = prefab.name;
            SceneManager.MoveGameObjectToScene(_root, _scene);
            foreach (var part in _root.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.hideFlags = HideFlags.DontSave;
            }

            _rig = _root.GetComponent<EffectRig>();
            _rig.Capture();
            RefreshGraphs();
            return true;
        }

        /// <summary>
        /// Bursts every graph at the pivot and starts the stream. A restart clears the particles still alive first;
        /// a loop leaves them, as the pool does when it bursts a rig again.
        /// </summary>
        public void Play(bool restart)
        {
            if (!HasRig)
            {
                return;
            }

            EndStream();
            if (restart)
            {
                foreach (var effect in _graphs)
                {
                    effect.Reinit();
                }
            }

            _rig.Burst(Pivot, Quaternion.identity, Scale, 1f);
            _stream = _rig.StartStream();
            RefreshGraphs();
            SincePlay = 0f;
            MoveStream(0f);
        }

        /// <summary>Moves the stream and sends events waiting for a graph's first run; the editor runs the graphs.</summary>
        public void Step(float deltaTime)
        {
            if (!HasRig)
            {
                return;
            }

            foreach (var effect in _graphs)
            {
                effect.pause = Paused;
            }

            if (!Paused)
            {
                SincePlay += deltaTime;
                MoveStream(deltaTime);
            }

            _rig.Tick();
        }

        /// <summary>Whether every graph is culled, which keeps it from running until a camera sees it.</summary>
        public bool AllCulled => Array.TrueForAll(_graphs, effect => effect.culled);

        /// <summary>Advances the paused graphs by <paramref name="seconds"/> at the next editor frame, moving the stream along.</summary>
        public void Simulate(float seconds)
        {
            var steps = (uint)Mathf.Max(1, Mathf.RoundToInt(seconds / StepSeconds));
            SincePlay += seconds;
            MoveStream(seconds);
            foreach (var effect in _graphs)
            {
                effect.pause = true;
                effect.Simulate(StepSeconds, steps);
            }
        }

        public void Dispose()
        {
            Unload();
            _environment.Dispose();
        }

        // Includes the copies a stream spawned beside the rig's own graphs.
        private void RefreshGraphs() => _graphs = _root.GetComponentsInChildren<VisualEffect>(true);

        private void Unload()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            _root = null;
            _rig = null;
            _stream = null;
            _graphs = Array.Empty<VisualEffect>();
        }

        private void EndStream()
        {
            if (_stream != null)
            {
                _rig.StopStream(_stream);
                _stream = null;
            }
        }

        private void MoveStream(float deltaTime)
        {
            if (_stream != null)
            {
                _stream.Move(PathPoint(SincePlay - deltaTime), PathPoint(SincePlay), Quaternion.identity, deltaTime, 1f, Scale);
            }
        }

        private Vector3 PathPoint(float time) =>
            Pivot + Vector3.right * (Mathf.PingPong(time * PathSpeed, PathLength) - PathLength * 0.5f);
    }
}
