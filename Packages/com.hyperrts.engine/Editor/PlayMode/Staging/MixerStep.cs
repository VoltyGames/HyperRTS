using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace HyperRTS.Editor.PlayMode.Staging
{
    /// <summary>
    /// Sets exposed audio mixer parameters by name, such as a channel's volume in decibels, so footage that gets its
    /// own music records without the match's.
    /// </summary>
    internal sealed class MixerStep : StagingStep
    {
        private readonly Dictionary<string, float> _values;

        private MixerStep(Dictionary<string, float> values) => _values = values;

        public override string Kind => "mixer";

        public static StagingStep Read(StagingObject step)
        {
            var json = step.Child("mixer");
            var values = json.Keys.ToDictionary(key => key, json.Number);
            return values.Count > 0 ? new MixerStep(values) : throw json.Problem("names no mixer parameter");
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
