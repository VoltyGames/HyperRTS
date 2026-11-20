using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace HyperRTS.Editor.PlayMode.Staging.Steps
{
    /// <summary>
    /// Sets exposed audio mixer parameters by name, such as a channel's volume in decibels, so footage that gets its
    /// own music records without the match's.
    /// </summary>
    internal sealed class MixerStep : StagingStep
    {
        private readonly Dictionary<string, float> _values;

        public MixerStep(Dictionary<string, float> values) => _values = values;

        public override void Check()
        {
            if (_values.Count == 0)
            {
                throw new InvalidOperationException("names no mixer parameter");
            }
        }

        public override void Run(StagingRun run)
        {
            var mixers = Resources.FindObjectsOfTypeAll<AudioMixer>();
            foreach (var (name, value) in _values)
            {
                // Every mixer that exposes the name takes the value; SetFloat answers false on the rest.
                var exposed = false;
                foreach (var mixer in mixers)
                {
                    exposed |= mixer.SetFloat(name, value);
                }

                if (!exposed)
                {
                    throw new InvalidOperationException($"HyperRTS: no loaded audio mixer exposes '{name}'.");
                }
            }
        }
    }
}
