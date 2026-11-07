using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Rendering.LitTeam
{
    /// <summary>HyperRTS/LitTeam's emission multiplier on a render entity, 0..1; without it meshes glow fully.</summary>
    [MaterialProperty("_LightsOn")]
    public struct LightsOn : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// A building root's lights: on while complete and powered, flickering for a moment whenever that changes.
    /// Client-only, written to <see cref="LightsOn"/> on every mesh under it.
    /// </summary>
    public struct BuildingLights : IComponentData
    {
        public const float FlickerSeconds = 0.8f;

        /// <summary>Brightness of a flicker's dim beats, so it stutters rather than blinks off.</summary>
        private const float Dim = 0.15f;

        public bool Lit;

        /// <summary>Seconds of flicker left.</summary>
        public float Flicker;

        /// <summary>The brightness last written to the meshes.</summary>
        public float Value;

        /// <summary>Lights as they stand when first seen, without a flicker.</summary>
        public static BuildingLights Seen(bool lit) => new() { Lit = lit, Value = lit ? 1f : 0f };

        /// <summary>Follows <paramref name="lit"/>; true when <see cref="Value"/> changed and needs writing.</summary>
        public bool Advance(bool lit, float deltaTime)
        {
            if (lit != Lit)
            {
                Lit = lit;
                Flicker = FlickerSeconds;
            }

            Flicker = math.max(Flicker - deltaTime, 0f);
            var value = Flicker > 0f ? Beat(Flicker) : Seen(Lit).Value;
            var changed = value != Value;
            Value = value;
            return changed;
        }

        // Two out-of-step waves give an uneven stutter like a failing ballast.
        private static float Beat(float left) => math.sin(left * 41f) + math.sin(left * 97f) > 0f ? 1f : Dim;
    }
}
