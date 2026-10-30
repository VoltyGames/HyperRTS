using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// One VFX Graph of a rig. Each burst sends its Burst event carrying the pose (position, normal, forward and scale),
    /// so bursts on the same frame keep their own, and the Density property thins it by effect detail. A graph drops
    /// events until it has run once, so bursts in the frame it was spawned wait for the next.
    /// </summary>
    public sealed class EffectGraph
    {
        public const string BurstEvent = "Burst";
        public const string DensityProperty = "Density";

        private static readonly int BurstId = Shader.PropertyToID(BurstEvent);
        private static readonly int DensityId = Shader.PropertyToID(DensityProperty);
        private static readonly int PositionId = Shader.PropertyToID("position");
        private static readonly int NormalId = Shader.PropertyToID("direction");
        private static readonly int ForwardId = Shader.PropertyToID("axisZ");
        private static readonly int ScaleXId = Shader.PropertyToID("scaleX");
        private static readonly int ScaleYId = Shader.PropertyToID("scaleY");
        private static readonly int ScaleZId = Shader.PropertyToID("scaleZ");

        private readonly VisualEffect _effect;
        private readonly VFXEventAttribute _attributes;
        private readonly int _spawnFrame = Time.frameCount;
        private readonly List<(Vector3 Position, Quaternion Rotation, float Scale)> _waiting = new();

        public EffectGraph(VisualEffect effect)
        {
            _effect = effect;
            _attributes = effect.CreateVFXEventAttribute();
        }

        private bool HasRun => Time.frameCount > _spawnFrame;

        /// <summary>Whether the graph has the Density property a rig thins it by.</summary>
        public static bool HasDensity(VisualEffect effect) => effect.HasFloat(DensityId);

        /// <summary>Sends the Burst event at the pose: +Y the normal, +Z the forward, uniform scale.</summary>
        public void Burst(Vector3 position, Quaternion rotation, float burstScale, float density)
        {
            _effect.SetFloat(DensityId, density);
            if (HasRun)
            {
                Send(position, rotation, burstScale);
            }
            else
            {
                _waiting.Add((position, rotation, burstScale));
            }
        }

        /// <summary>Sends the bursts that waited for the graph's first run.</summary>
        public void Tick()
        {
            if (_waiting.Count == 0 || !HasRun)
            {
                return;
            }

            foreach (var burst in _waiting)
            {
                Send(burst.Position, burst.Rotation, burst.Scale);
            }

            _waiting.Clear();
        }

        private void Send(Vector3 position, Quaternion rotation, float burstScale)
        {
            _attributes.SetVector3(PositionId, position);
            _attributes.SetVector3(NormalId, rotation * Vector3.up);
            _attributes.SetVector3(ForwardId, rotation * Vector3.forward);
            // scale is stored per axis, so a Vector3 under its own name never reaches the graph.
            _attributes.SetFloat(ScaleXId, burstScale);
            _attributes.SetFloat(ScaleYId, burstScale);
            _attributes.SetFloat(ScaleZId, burstScale);
            _effect.SendEvent(BurstId, _attributes);
        }
    }
}
