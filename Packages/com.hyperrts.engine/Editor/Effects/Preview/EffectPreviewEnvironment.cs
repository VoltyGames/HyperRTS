using System;
using UnityEngine;

namespace HyperRTS.Editor.Effects.Preview
{
    /// <summary>
    /// The globals effect graphs read while previewing: wind, and fog of war held clear. The values found are put back
    /// on dispose.
    /// </summary>
    public sealed class EffectPreviewEnvironment : IDisposable
    {
        private static readonly int FogColorId = Shader.PropertyToID("_HyperRTSFogOfWarColor");
        private static readonly int WindDirectionId = Shader.PropertyToID("_HyperRTSWindDirection");
        private static readonly int WindStrengthId = Shader.PropertyToID("_HyperRTSWindStrength");

        private readonly Vector4 _fogColor = Shader.GetGlobalVector(FogColorId);
        private readonly Vector4 _windDirection = Shader.GetGlobalVector(WindDirectionId);
        private readonly float _windStrength = Shader.GetGlobalFloat(WindStrengthId);

        public EffectPreviewEnvironment()
        {
            // A fog colour with alpha 0 is how the shaders read "no fog shown".
            Shader.SetGlobalVector(FogColorId, Vector4.zero);
            SetWind(0f, 0f);
        }

        /// <summary>Blows towards <paramref name="degrees"/> clockwise from +Z on the ground, at <paramref name="strength"/>.</summary>
        public void SetWind(float degrees, float strength)
        {
            var heading = Quaternion.Euler(0f, degrees, 0f) * Vector3.forward;
            Shader.SetGlobalVector(WindDirectionId, heading);
            Shader.SetGlobalFloat(WindStrengthId, strength);
        }

        public void Dispose()
        {
            Shader.SetGlobalVector(FogColorId, _fogColor);
            Shader.SetGlobalVector(WindDirectionId, _windDirection);
            Shader.SetGlobalFloat(WindStrengthId, _windStrength);
        }
    }
}
