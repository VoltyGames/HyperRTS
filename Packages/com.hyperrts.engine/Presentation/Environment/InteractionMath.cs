using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Units;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>Sizes, timing and stamp shapes of the interaction maps (trampling, track marks, scars and wakes).</summary>
    public static class InteractionMath
    {
        /// <summary>Largest side of either map; bigger maps get coarser texels.</summary>
        public const int MaxTextureSize = 8192;

        /// <summary>Steps an 8-bit channel fades through.</summary>
        public const int Levels = 255;

        public const float TrampleRecoverySeconds = 60f;
        public const float TrackFadeSeconds = 240f;
        public const float WakeFadeSeconds = 5f;

        /// <summary>Metres at full scale; deeper craters clip. Matches Trample.hlsl.</summary>
        public const float MaxCraterDepth = 2f;

        public const float ScorchReach = 1.25f;

        public const float MinWakeSpeed = 0.5f;

        public const float FullWakeSpeed = 8f;

        public const float WakeReachSeconds = 0.5f;

        /// <summary>A vehicle this far above the terrain (on a bridge or deck) leaves no marks on it.</summary>
        public const float MaxGroundGap = 1f;

        /// <summary>Movement in one frame past this is a teleport, not a drive.</summary>
        public const float MaxStep = 5f;

        /// <summary>Half width in metres of the strip one soldier treads, and how far it bends plants and crops.</summary>
        public const float FootWidth = 0.3f;
        public const float FootFlatten = 0.4f;
        public const float FootCropDamage = 0.15f;

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

        /// <summary>Whole 1/255 fade steps due for a full fade over <paramref name="duration"/>. <paramref name="carry"/> keeps the remainder.</summary>
        public static int FadeSteps(ref float carry, float deltaTime, float duration)
        {
            carry += deltaTime * Levels / duration;
            var steps = (int)math.floor(carry);
            carry -= steps;
            return steps;
        }

        /// <summary>A vehicle's marks this frame, stretched back over <paramref name="advance"/> metres so fast vehicles leave no gaps.</summary>
        public static void AddVehicle(in TrackMarks marks, float2 position, float2 forward, float advance,
            List<InteractionStamp> trample, List<InteractionStamp> tracks)
        {
            AddTrample(marks, position, forward, advance, trample);
            AddRuts(marks, position, forward, advance, tracks);
        }

        /// <summary>The grass flattened across the vehicle's whole width this frame.</summary>
        public static void AddTrample(in TrackMarks marks, float2 position, float2 forward, float advance,
            List<InteractionStamp> trample)
        {
            var center = position - forward * (advance * 0.5f);
            var span = (marks.Gauge + marks.Width) * 0.5f;
            var halfLength = (marks.Width + math.abs(advance)) * 0.5f;
            trample.Add(Stamp(center, forward, new float2(span, math.max(halfLength, span)), new float4(1f, 1f, 1f, 0f)));
        }

        /// <summary>A rut under each side of one axle, or of the tracks, heading along <paramref name="forward"/>.</summary>
        public static void AddRuts(in TrackMarks marks, float2 axle, float2 forward, float advance,
            List<InteractionStamp> tracks)
        {
            var center = axle - forward * (advance * 0.5f);
            var halfLength = (marks.Width + math.abs(advance)) * 0.5f;
            var rut = new float4(marks.RutDepth, 0.5f + 0.5f * marks.RutDepth, 0f, 0f);
            var halfSize = new float2(marks.Width * 0.5f, halfLength);
            var offset = Right(forward) * (marks.Gauge * 0.5f);
            tracks.Add(Stamp(center - offset, forward, halfSize, rut));
            tracks.Add(Stamp(center + offset, forward, halfSize, rut));
        }

        /// <summary>A soldier's path this frame: a narrow strip that leans plants along the way, without flattening them.</summary>
        public static InteractionStamp Footsteps(float2 feet, float2 forward, float stride) =>
            Stamp(feet - forward * (stride * 0.5f), forward, new float2(FootWidth, FootWidth + stride * 0.5f),
                new float4(FootFlatten, FootCropDamage, 1f, 0f));

        /// <summary>A building's footprint: plants under it stay flat and crops never come back.</summary>
        public static InteractionStamp Footprint(float2 center, float2 size) =>
            Stamp(center, new float2(0f, 1f), size * 0.5f, new float4(1f, 1f, 0f, 0f));

        /// <summary>The unit vector to the right of <paramref name="forward"/> on the ground.</summary>
        public static float2 Right(float2 forward) => new(forward.y, -forward.x);

        private static InteractionStamp Stamp(float2 center, float2 forward, float2 halfSize, float4 value) =>
            new() { Center = center, Forward = forward, HalfSize = halfSize, Value = value };
    }
}
