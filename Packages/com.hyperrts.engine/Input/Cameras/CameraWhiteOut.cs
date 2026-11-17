using System;
using System.Collections.Generic;
using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Simulation.Common.Authoring;
using UnityEngine;
using UnityEngine.Rendering;

namespace HyperRTS.Input.Cameras
{
    /// <summary>
    /// The screen's white-out from blinding bursts: a volume whose profile overexposes and blooms the picture, weighted
    /// by the brightest burst seen, rising at once and fading slowly.
    /// </summary>
    [Serializable]
    public class CameraWhiteOut
    {
        [Tooltip("Blinding bursts white out the screen; a player setting.")]
        public bool enabled = true;

        [Tooltip("Volume whose profile is the full white-out (exposure, bloom); its weight follows the flash.")]
        public Volume volume;

        [Tooltip("Seconds the screen takes to recover from a full white-out.")]
        [Unit("s")]
        [Min(0.01f)]
        public float recoverySeconds = 5f;

        /// <summary>The white-out's share this frame, from 0 to 1.</summary>
        public float Level { get; private set; }

        /// <summary>Takes the presented world's new pulses, then fades; while not <see cref="enabled"/> it shows none.</summary>
        public void Step(Vector3 focus, float deltaTime)
        {
            if (!enabled)
            {
                Level = 0f;
            }
            else if (TryGetWorldPulses(out var pulses))
            {
                foreach (var pulse in pulses)
                {
                    Add(pulse, focus);
                }
            }

            Advance(deltaTime);
        }

        /// <summary>Raises the level at once to the pulse's, weakened by its distance from <paramref name="focus"/>.</summary>
        public void Add(in WhiteOutPulse pulse, Vector3 focus)
        {
            var falloff = 1f - Vector3.Distance(pulse.Position, focus) / Mathf.Max(pulse.Reach, 0.01f);
            Level = Mathf.Max(Level, Mathf.Clamp01(pulse.Strength * falloff));
        }

        /// <summary>Fades the level, slowly at first and quicker as the eye adjusts, and weights the volume.</summary>
        public void Advance(float deltaTime)
        {
            Level = Mathf.Max(0f, Level - deltaTime / recoverySeconds * (0.25f + Level));
            if (volume != null)
            {
                volume.weight = Level;
            }
        }

        private static bool TryGetWorldPulses(out List<WhiteOutPulse> pulses)
        {
            pulses = null;
            if (!DefaultWorld.TryGet(out var world))
            {
                return false;
            }

            pulses = world.GetExistingSystemManaged<EffectPoolSystem>()?.Pool.WhiteOuts;
            return pulses != null;
        }
    }
}
