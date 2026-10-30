using UnityEngine;
using UnityEngine.VFX;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// One stream's copy of a rig's stream graphs: the Play event starts their rate layers, properties carry this
    /// frame's path, pose, speed and density, and the Stop event ends emission while live particles finish their lives.
    /// </summary>
    public sealed class GraphStream
    {
        public const string PlayEvent = "Play";
        public const string StopEvent = "Stop";
        public const string FromProperty = "From";
        public const string ToProperty = "To";
        public const string UpProperty = "Up";
        public const string ForwardProperty = "Forward";
        public const string ScaleProperty = "Scale";
        public const string SpeedProperty = "Speed";

        private static readonly int PlayId = Shader.PropertyToID(PlayEvent);
        private static readonly int StopId = Shader.PropertyToID(StopEvent);
        private static readonly int FromId = Shader.PropertyToID(FromProperty);
        private static readonly int ToId = Shader.PropertyToID(ToProperty);
        private static readonly int UpId = Shader.PropertyToID(UpProperty);
        private static readonly int ForwardId = Shader.PropertyToID(ForwardProperty);
        private static readonly int ScaleId = Shader.PropertyToID(ScaleProperty);
        private static readonly int SpeedId = Shader.PropertyToID(SpeedProperty);
        private static readonly int DensityId = Shader.PropertyToID(EffectGraph.DensityProperty);

        private readonly VisualEffect[] _effects;
        private int _startFrame;
        private bool _started;
        private bool _playing;

        public GraphStream(VisualEffect[] effects) => _effects = effects;

        /// <summary>Whether the graph streams by rate: the builder exposes Speed only on graphs with rate layers.</summary>
        public static bool Streams(VisualEffect effect) => effect.HasFloat(SpeedId);

        /// <summary>Stopped, and every particle it emitted has died, so it may serve another stream.</summary>
        public bool IsDone => !_playing && (!_started || !AnyAwake());

        /// <summary>Starts emitting; the Play event goes out once the graphs have run, since they drop earlier events.</summary>
        public void Start()
        {
            _startFrame = Time.frameCount;
            _started = false;
            _playing = true;
        }

        /// <summary>Emits along this frame's path at the pose; density carries the loop's strength and effect detail.</summary>
        public void Move(Vector3 from, Vector3 to, Quaternion rotation, float deltaTime, float density, float poseScale)
        {
            var speed = deltaTime > 0f ? Vector3.Distance(from, to) / deltaTime : 0f;
            foreach (var effect in _effects)
            {
                effect.SetVector3(FromId, from);
                effect.SetVector3(ToId, to);
                effect.SetVector3(UpId, rotation * Vector3.up);
                effect.SetVector3(ForwardId, rotation * Vector3.forward);
                effect.SetFloat(ScaleId, poseScale);
                effect.SetFloat(SpeedId, speed);
                effect.SetFloat(DensityId, density);
            }

            if (!_started && Time.frameCount > _startFrame)
            {
                Send(PlayId);
                _started = true;
            }
        }

        /// <summary>Ends emission; live particles fade out over the rest of their lives.</summary>
        public void Stop()
        {
            _playing = false;
            if (_started)
            {
                Send(StopId);
            }
        }

        private void Send(int eventId)
        {
            foreach (var effect in _effects)
            {
                effect.SendEvent(eventId);
            }
        }

        private bool AnyAwake()
        {
            foreach (var effect in _effects)
            {
                if (effect.HasAnySystemAwake())
                {
                    return true;
                }
            }

            return false;
        }
    }
}
