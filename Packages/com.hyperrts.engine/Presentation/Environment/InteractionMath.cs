using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Units;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>Sizes, timing and stamp shapes of the interaction maps (trampling and track marks).</summary>
    public static class InteractionMath
    {
        /// <summary>Largest side of either map; bigger maps get coarser texels.</summary>
        public const int MaxTextureSize = 8192;

        /// <summary>Steps an 8-bit channel fades through; each fade tick takes one.</summary>
        public const int Levels = 255;

        public const float TrampleRecoverySeconds = 60f;
        public const float TrackFadeSeconds = 240f;

        /// <summary>Smallest splash radius whose crater flattens the ground around it.</summary>
        public const float BigCraterRadius = 3f;

        /// <summary>A vehicle this far above the terrain (on a bridge or deck) leaves no marks on it.</summary>
        public const float MaxGroundGap = 1f;

        /// <summary>Movement in one frame past this is a teleport, not a drive.</summary>
        public const float MaxStep = 5f;

        public static float TracksTexel(InteractionQuality quality) => quality switch
        {
            InteractionQuality.Full => 0.2f,
            InteractionQuality.Low => 0.4f,
            _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, "Off has no interaction maps."),
        };

        public static float TrampleTexel(InteractionQuality quality) => quality switch
        {
            InteractionQuality.Full => 0.5f,
            InteractionQuality.Low => 1f,
            _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, "Off has no interaction maps."),
        };

        /// <summary>Texels needed to cover <paramref name="mapSize"/> metres, capped at <see cref="MaxTextureSize"/>.</summary>
        public static int2 TextureSize(float2 mapSize, float texel) =>
            math.clamp((int2)math.ceil(mapSize / texel), 1, MaxTextureSize);

        /// <summary>The rect shaders read: (x min, z min, 1 / width, 1 / depth).</summary>
        public static float4 Rect(float2 min, float2 size) => new(min, 1f / size);

        public static float2 WorldToUV(float4 rect, float2 positionXZ) => (positionXZ - rect.xy) * rect.zw;

        /// <summary>
        /// Whole 1/255 fade steps due after <paramref name="deltaTime"/>, so a channel goes from full to empty in
        /// <paramref name="duration"/> seconds; <paramref name="carry"/> keeps the fraction for the next frame.
        /// </summary>
        public static int FadeSteps(ref float carry, float deltaTime, float duration)
        {
            carry += deltaTime * Levels / duration;
            var steps = (int)math.floor(carry);
            carry -= steps;
            return steps;
        }

        /// <summary>
        /// A vehicle's marks this frame: a rut under each track and flattened, crushed plants under its width. They
        /// reach back over <paramref name="advance"/> metres driven along <paramref name="forward"/> (negative when
        /// reversing), so fast vehicles leave no gaps between frames.
        /// </summary>
        public static void AddVehicle(in TrackMarks marks, float2 position, float2 forward, float advance,
            List<InteractionStamp> trample, List<InteractionStamp> tracks)
        {
            var center = position - forward * (advance * 0.5f);
            var halfLength = (marks.Width + math.abs(advance)) * 0.5f;
            var right = Right(forward);
            var span = (marks.Gauge + marks.Width) * 0.5f;
            trample.Add(Stamp(center, forward, new float2(span, math.max(halfLength, span)), new float4(1f, 1f, 1f, 0f)));

            var rut = new float4(marks.RutDepth, 0.5f + 0.5f * marks.RutDepth, 0f, 0f);
            var halfSize = new float2(marks.Width * 0.5f, halfLength);
            var offset = right * (marks.Gauge * 0.5f);
            tracks.Add(Stamp(center - offset, forward, halfSize, rut));
            tracks.Add(Stamp(center + offset, forward, halfSize, rut));
        }

        /// <summary>A building's footprint: plants under it stay flat and crops never come back.</summary>
        public static InteractionStamp Footprint(float2 center, float2 size) =>
            Stamp(center, new float2(0f, 1f), size * 0.5f, new float4(1f, 1f, 0f, 0f));

        /// <summary>A big crater: flattened plants and churned soil in a disc of <paramref name="radius"/>.</summary>
        public static void AddCrater(float2 center, float radius, List<InteractionStamp> trample,
            List<InteractionStamp> tracks)
        {
            var halfSize = new float2(radius);
            var north = new float2(0f, 1f);
            trample.Add(Stamp(center, north, halfSize, new float4(1f, 1f, 0f, 1f)));
            tracks.Add(Stamp(center, north, halfSize, new float4(0f, 1f, 0f, 1f)));
        }

        /// <summary>The unit vector to the right of <paramref name="forward"/> on the ground.</summary>
        public static float2 Right(float2 forward) => new(forward.y, -forward.x);

        private static InteractionStamp Stamp(float2 center, float2 forward, float2 halfSize, float4 value) =>
            new() { Center = center, Forward = forward, HalfSize = halfSize, Value = value };
    }
}
